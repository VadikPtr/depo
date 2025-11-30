using System.Runtime.InteropServices;

namespace DepoBCS;

internal class NinjaGenerator : IDisposable {
  public readonly  string          project_file;
  private readonly ProjectM        _project;
  private readonly SolutionContext _ctx;
  private readonly FileStream      _stream;
  private readonly StreamWriter    _writer;
  private readonly HashSet<string> _link_libs  = [];
  private readonly HashSet<string> _link_flags = [];
  private readonly HashSet<string> _cflags     = [];

  public NinjaGenerator(ProjectM project, SolutionContext ctx) {
    project_file = Path.Join(ctx.build_directory, $"{project.name}.ninja");
    _project     = project;
    _ctx         = ctx;
    _stream      = new FileStream(project_file, FileMode.Create, FileAccess.Write);
    _writer      = new StreamWriter(_stream);
  }

  public void Dispose() {
    _writer?.Dispose();
    _stream?.Dispose();
  }

  public void write() {
    _writer.Write("ninja_required_version = 1.6\n\n");
    collect_cflags();
    write_compile_rules();
    if (_project.kind is Kind.Exe or Kind.Dll) {
      collect_link_flags(_project);
      write_link_rules();
    }
    write_targets();
  }

  private void write_targets() {
    var output_path = project_output_files(_project.name, _project.kind);

    var file_targets = _project.files
      .Select(src => (src.path_escape_ninja(), dst: get_obj_path(src), rule: detect_rule(src)))
      .Where(x => x.rule != null)
      .ToArray();

    foreach (var (src, dst, rule) in file_targets) {
      _writer.Write($"build {dst}: {rule} {src}\n");
    }

    var objs = string.Join(" $\n  ", file_targets.Select(x => x.dst));
    _writer.Write("\n");

    if (_project.kind is Kind.Exe) {
      var libs = string.Join(" $\n  ", _link_libs.Select(x => x.path_escape_ninja()));
      _writer.Write($"build {output_path}: link {objs} {libs}\n");
      _writer.Write($"  linked = {output_path}\n\n");
    } else if (_project.kind is Kind.Dll) {
      var implib = project_output_files(_project.name, Kind.Lib);
      var libs   = string.Join(" $\n  ", _link_libs.Select(x => x.path_escape_ninja()));
      _writer.Write($"build {output_path} {implib}: link {objs} {libs}\n");
      _writer.Write($"  linked = {output_path}\n\n");
    } else if (_project.kind is Kind.Lib) {
      _writer.Write($"build {output_path}: ar {objs}\n\n");
    }

    _writer.Write($"build {_project.name}: phony {output_path}\n\n");
  }

  private string get_obj_path(string source_path) {
    var relative_path = Path.GetRelativePath(relativeTo: _ctx.model.dir, source_path);
    var full_path     = Path.Join(_ctx.obj_directory, relative_path);
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

  private string project_output_files(string name, Kind kind) {
    var target      = name.wrap(kind);
    var output_path = Path.Join(_ctx.bin_directory, target);
    var result      = output_path.path_escape_ninja();
    if (kind is Kind.Dll) {
      // result += " | " + project_output_files(name, Kind.Lib);
      // result += " " + project_output_files(name, Kind.Lib);
    }
    return result;
  }

  private void write_compile_rules() {
    var cflags_str = string.Join(' ', _cflags);
    _writer.Write(
      $"""
      rule cc
        command = clang {cflags_str} -std=c11 -x c -MF $out.d -c -o $out $in
        description = cc $out
        deps = gcc
        depfile = $out.d

      rule cxx
        command = clang++ {cflags_str} -std=c++20 -x c++ -MF $out.d -c -o $out $in
        description = cxx $out
        deps = gcc
        depfile = $out.d

      rule ar
        command = llvm-ar rcs $out $in
        description = ar $out
      """
    );
    _writer.Write("\n\n");
  }

  private void write_link_rules() {
    var flags = string.Join(' ', _link_flags);
    _writer.Write(
      $"""
      rule link
        command = clang++ {flags} -o $linked $in
        description = link $out
      """
    );
    _writer.Write("\n\n");
  }

  private IEnumerable<ProjectM> projects_with_names(IEnumerable<string> names) {
    foreach (var name in names) {
      foreach (var lib_project in _ctx.model.projects) {
        if (lib_project.name == name) {
          yield return lib_project;
        }
      }
    }
  }

  private void collect_cflags() {
    _cflags.Add("-fdiagnostics-color=always");
    _cflags.Add("--write-dependencies"); // Write a depfile containing user and system headers 
    _cflags.Add("-MP"); // Create phony target for each dependency (other than main file)

    switch (_ctx.config) {
      case BuildConfig.Debug:
        _cflags.Add("-g");
        _cflags.Add("-O0");
        _cflags.Add("-DDEBUG");
        _cflags.Add("-D_DEBUG");
        break;
      case BuildConfig.Release:
        _cflags.Add("-O3");
        _cflags.Add("-DNDEBUG");
        break;
      default:
        throw new ArgumentOutOfRangeException();
    }

    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
      _cflags.Add("-D_CRT_SECURE_NO_WARNINGS");
    }

    collect_includes(_project);
    collect_definitions(_project);
  }

  private void collect_includes(ProjectM proj) {
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
        _cflags.Add($"-I{value}");
      }
    }

    foreach (var link in proj.link) {
      if ((link.flags & LinkFlags.Prj) == 0) {
        continue; // do not care
      }
      foreach (var lib in projects_with_names(link.libs)) {
        collect_includes(lib);
      }
    }
  }

  private void collect_definitions(ProjectM proj) {
    bool is_current_project = proj == _project;

    if (is_current_project && proj.kind == Kind.Dll) {
      _cflags.Add("-D_DLL");
    }

    foreach (var def in proj.cflags) {
      bool is_shared = def.flags != VisibilityFlags.None;
      if (!is_current_project && !is_shared) {
        continue;
      }
      if (is_current_project && (def.flags & VisibilityFlags.Iface) != 0) {
        continue;
      }
      foreach (var value in def.values) {
        _cflags.Add(value);
      }
    }

    foreach (var link in proj.link) {
      if ((link.flags & LinkFlags.Prj) == 0) {
        continue; // do not care
      }
      foreach (var lib in projects_with_names(link.libs)) {
        collect_definitions(lib);
      }
    }
  }

  private void collect_link_flags(ProjectM proj) {
    bool is_current_project = proj == _project;

    if (is_current_project && proj.kind == Kind.Dll) {
      _link_flags.Add("-shared");
      _link_flags.Add("-Wl,/NODEFAULTLIB:libcmt");
      switch (_ctx.config) {
        case BuildConfig.Debug:
          _link_flags.Add("-lmsvcrtd.lib");
          // _link_flags.Add("-lvcruntimed.lib");
          // _link_flags.Add("-lucrtd.lib");
          break;
        case BuildConfig.Release:
          _link_flags.Add("-lmsvcrt.lib");
          // _link_flags.Add("-lvcruntime.lib");
          // _link_flags.Add("-lucrt.lib");
          break;
        default:
          throw new ArgumentOutOfRangeException();
      }
    }

    foreach (var link in proj.link) {
      bool is_shared = link.visibility != VisibilityFlags.None;
      if (!is_current_project && !is_shared) {
        continue;
      }

      if ((link.flags & LinkFlags.Prj) != 0) {
        foreach (var lib in projects_with_names(link.libs)) {
          if (lib.kind is Kind.Lib or Kind.Dll) {
            collect_link_flags(lib);
            _link_libs.Add(Path.Join(_ctx.bin_directory, lib.name.wrap(Kind.Lib)));
          } else if (lib.kind is Kind.Iface) {
            collect_link_flags(lib);
          }
        }
        continue;
      }

      foreach (var lib in link.libs) {
        if ((link.flags & LinkFlags.Sys) != 0) {
          _link_flags.Add($"-l{lib}");
        } else {
          var path = Path.Join(proj.depo.dir, lib);
          _link_flags.Add($"-l{path}");
        }
      }
    }
  }
}
