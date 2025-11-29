using System.Runtime.InteropServices;

namespace DepoBCS;

internal static class NinjaExt {
  public static string path_escape_ninja(this string path) {
    return path.Replace(":", "$:");
  }
}

internal class NinjaProject : IDisposable {
  private readonly ProjectM   _project;
  private readonly Ninja      _ninja;
  private readonly FileStream _stream;
  private readonly TextWriter _writer;
  public readonly  string     project_file;

  public NinjaProject(ProjectM project, Ninja ninja) {
    project_file = Path.Join(ninja.build_directory, $"{project.name}.ninja");
    _project     = project;
    _ninja       = ninja;
    _stream      = new FileStream(project_file, FileMode.Create, FileAccess.Write);
    _writer      = new StreamWriter(_stream);
  }

  public void Dispose() {
    _writer?.Dispose();
    _stream?.Dispose();
  }

  public void write() {
    Console.WriteLine($"{project_file} written");
    _writer.Write("ninja_required_version = 1.6\n\n");
    write_rules();
    write_targets();
  }

  private void write_targets() {
    var output_path = project_output_file(_project);

    var file_targets = _project.files
      .Select(src => (src.path_escape_ninja(), dst: get_obj_path(src), rule: detect_rule(src)))
      .Where(x => x.rule != null)
      .ToArray();

    foreach (var (src, dst, rule) in file_targets) {
      _writer.Write($"build {dst}: {rule} {src}\n");
    }

    var objs = string.Join(' ', file_targets.Select(x => x.dst));
    _writer.Write("\n");
    _writer.Write($"build {output_path}: link {objs}\n\n");
    _writer.Write($"build {_project.name}: phony {output_path}\n\n");
  }

  private string get_obj_path(string source_path) {
    var relative_path = Path.GetRelativePath(relativeTo: _ninja.model.dir, source_path);
    var full_path     = Path.Join(_ninja.obj_directory, relative_path);
    return Path.ChangeExtension(full_path, ".o").path_escape_ninja();
  }

  private static string detect_rule(string file_name) {
    if (file_name.EndsWith(".cpp") || file_name.EndsWith(".cc")) {
      return "cxx";
    }
    if (file_name.EndsWith(".c")) {
      return "cc";
    }
    Console.WriteLine($"No known rule to make {file_name}, skipping");
    return null;
  }

  private string project_output_file(ProjectM project) {
    var (prefix, suffix) = target_platform_appends(project);
    var output_path = Path.Join(_ninja.bin_directory, prefix + project.name + suffix);
    return output_path.path_escape_ninja();
  }

  private static (string prefix, string suffix) target_platform_appends(ProjectM proj) {
    switch (proj.kind) {
      case Kind.Dll:
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
          return ("", ".dll");
        }
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) {
          return ("lib", ".so");
        }
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) {
          return ("lib", ".dylib");
        }
        throw new ArgumentOutOfRangeException();
      case Kind.Lib:
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
          return ("", ".lib");
        }
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ||
            RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) {
          return ("lib", ".a");
        }
        throw new ArgumentOutOfRangeException();
      case Kind.Exe:
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
          return ("", ".exe");
        }
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ||
            RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) {
          return ("", "");
        }
        throw new ArgumentOutOfRangeException();
      default:
        throw new ArgumentOutOfRangeException();
    }
  }

  private void write_rules() {
    var flags      = generate_flags();
    var flags_cc   = string.Join(' ', flags);
    var flags_cxx  = string.Join(' ', flags);
    var link_flags = "TODO";
    Console.WriteLine($"{_project.name} cflags: {flags_cc}");

    _writer.Write(
      $"""
      rule cc
        command = clang {flags_cc} -std=c11 -x c -MF $out.d -c -o $out $in
        description = cc $out
        deps = gcc
        depfile = $out.d

      rule cxx
        command = clang++ {flags_cxx} -std=c++20 -x c++ -MF $out.d -c -o $out $in
        description = cxx $out
        deps = gcc
        depfile = $out.d

      rule link
        command = clang++ -o $out $in {link_flags}
        description = link $out
      """);
    _writer.Write("\n");
    _writer.Write("\n");
  }

  private HashSet<string> generate_flags() {
    var flags = new HashSet<string> {
      "-fdiagnostics-color=always",
      "--write-dependencies", // Write a depfile containing user and system headers 
      "-MP", // Create phony target for each dependency (other than main file)
    };

    switch (_ninja.config) {
      case BuildConfig.Debug:
        flags.Add("-g");
        flags.Add("-O0");
        flags.Add("-DDEBUG");
        flags.Add("-D_DEBUG");
        break;
      case BuildConfig.Release:
        flags.Add("-O3");
        flags.Add("-DNDEBUG");
        break;
      default:
        throw new ArgumentOutOfRangeException();
    }

    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
      flags.Add("-D_CRT_SECURE_NO_WARNINGS");
    }

    collect_includes(_project, flags);
    collect_flags(_project, flags);

    return flags;
  }

  private void collect_includes(ProjectM proj, HashSet<string> flags) {
    bool is_current_project = proj == _project;

    foreach (var include in proj.include) {
      bool is_shared = include.flags != VisibilityFlags.None;
      if (!is_current_project && !is_shared) {
        continue;
      }
      if (is_current_project && (include.flags & VisibilityFlags.Iface) != 0) {
        continue;
      }
      foreach (var value in include.dirs) {
        flags.Add($"-I{value}");
      }
    }

    foreach (var link in proj.link) {
      if ((link.flags & LinkFlags.Prj) == 0) {
        continue; // do not care
      }
      foreach (var lib_name in link.libs) {
        foreach (var lib_project in _ninja.model.projects) {
          if (lib_project.name == lib_name) {
            collect_includes(lib_project, flags);
          }
        }
      }
    }
  }

  private void collect_flags(ProjectM proj, HashSet<string> flags) {
    bool is_current_project = proj == _project;

    foreach (var def in proj.cflags) {
      bool is_shared = def.flags != VisibilityFlags.None;
      if (!is_current_project && !is_shared) {
        continue;
      }
      if (is_current_project && (def.flags & VisibilityFlags.Iface) != 0) {
        continue;
      }
      foreach (var value in def.values) {
        flags.Add(value);
      }
    }

    // TODO:
    // foreach (var link in proj.link) {
    //   // if (proj.kind == Kind.Lib) {
    //   //   // collect_flags();
    //   // }
    // }
  }
}

internal enum BuildConfig {
  Debug,
  Release,
}

internal class Ninja {
  public readonly DepoM       model;
  public readonly BuildConfig config;
  public readonly string      build_directory;
  public readonly string      bin_directory;
  public readonly string      obj_directory;

  public Ninja(DepoM model, BuildConfig config) {
    this.model      = model;
    this.config     = config;
    build_directory = Path.Join(model.dir, "build", config.ToString());
    bin_directory   = Path.Join(model.dir, "bin", config.ToString());
    obj_directory   = Path.Join(build_directory, "obj");
    Directory.CreateDirectory(build_directory);
    Directory.CreateDirectory(obj_directory);
    Console.WriteLine($"Build directory: {build_directory}");
  }

  public void generate() {
    List<NinjaProject> projects = [];
    try {
      foreach (var model_project in model.projects) {
        projects.Add(new NinjaProject(model_project, this));
      }
      foreach (var project in projects) {
        project.write();
      }
    } finally {
      foreach (var project in projects) {
        project.Dispose();
      }
    }

    using var file   = File.Open(Path.Join(build_directory, "build.ninja"), FileMode.Create, FileAccess.Write);
    using var writer = new StreamWriter(file);
    foreach (var project in projects) {
      writer.Write($"subninja ./{Path.GetFileName(project.project_file)}\n");
    }
    if (model.targets.Length != 0) {
      writer.Write($"default {model.targets[0]}\n");
    }
    writer.Close();
  }
}
