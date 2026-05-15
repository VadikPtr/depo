using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text.Json.Serialization;

namespace depo;

[Flags]
internal enum BuildConfig {
  None    = 0b000, // same as any
  Dbg     = 0b001,
  Rel     = 0b010,
  Debug   = 0b001,
  Release = 0b010,
}

internal enum Kind : uint {
  Dll,
  Lib,
  Exe,
  Iface,
}

[Flags]
internal enum OS : uint {
  None = 0b000,
  Win  = 0b001,
  Lin  = 0b010,
  Mac  = 0b100,
}

[Flags]
internal enum VisibilityFlags : uint {
  None  = 0b000,
  Pub   = 0b001,
  Iface = 0b010, // apply only for linked targets
}

[Flags]
internal enum LinkFlags : uint {
  None = 0b000,
  Prj  = 0b010,
  Sys  = 0b100,
}

internal record Include(VisibilityFlags flags, string[] dirs);

internal record CFlags(VisibilityFlags flags, string[] values);

internal record Link(VisibilityFlags visibility, LinkFlags flags, string[] libs);

internal record LinkDir(VisibilityFlags visibility, string[] dirs);

internal class ProjectM {
  [JsonIgnore] public DepoM         depo;
  public              string        name;
  public              Kind          kind;
  public              HashSet<string>  files     = [];
  public              List<Include> include   = [];
  public              List<Link>    link      = [];
  public              List<LinkDir> link_dirs = [];
  public              List<CFlags>  cflags    = [];
}

internal class DependencyM {
  public string name;
  public string url;
  public string branch;
}

internal class CustomCommandM {
  public string   name;
  public string[] args;
}

internal class DepoM {
  public string               dir;
  public string[]             require         = [];
  public string[]             targets         = [];
  public List<string>         bin             = [];
  public List<ProjectM>       projects        = [];
  public List<DependencyM>    git_deps        = [];
  public List<DependencyM>    svn_deps        = [];
  public List<DependencyM>    archive_deps    = [];
  public List<CustomCommandM> custom_commands = [];
  public BuildConfig          build_config;
}

internal interface IDepoMAction {
  void execute(DepoM model);
}

internal interface IProjectMAction {
  void execute(ProjectM model);
}

internal class KindAction(AstNode[] args) : IProjectMAction {
  public void execute(ProjectM model) {
    var arg = args.unpack_as_string();
    if (!Enum.TryParse(arg, ignoreCase: true, out Kind kind)) {
      throw new InvalidOperationException($"Invalid kind value: {arg}!");
    }
    model.kind = kind;
  }
}

internal class FilesAction(AstNode[] args) : IProjectMAction {
  public void execute(ProjectM model) {
    if (!args.check_os_flags()) {
      return;
    }
    var patterns = args.unpack_as_string_array_skip_flags();
    foreach (var pattern in patterns) {
      if (pattern.Contains('*')) {
        foreach (var f in Directory.EnumerateFiles(model.depo.dir, pattern, SearchOption.AllDirectories)
            .Select(PathLib.normalize)) {
          model.files.Add(f);
        }
      } else {
        var full_path = PathLib.normalize(Path.Join(model.depo.dir, pattern));
        model.files.Add(full_path);
      }
    }
  }
}

internal class ExcludeFilesAction(AstNode[] args) : IProjectMAction {
  public void execute(ProjectM model) {
    if (!args.check_os_flags()) {
      return;
    }
    var patterns = args.unpack_as_string_array_skip_flags();
    foreach (var pattern in patterns) {
      if (pattern.Contains('*')) {
        foreach (var f in Directory.EnumerateFiles(model.depo.dir, pattern, SearchOption.AllDirectories)
            .Select(PathLib.normalize)) {
          if (model.files.Remove(f)) {
            Log.debug("Exclude file: {0}", f);
          }
        }
      } else {
        var full_path = PathLib.normalize(Path.Join(model.depo.dir, pattern));
        if (model.files.Remove(full_path)) {
          Log.debug("Exclude file: {0}", full_path);
        }
      }
    }
  }
}

internal class IncludeAction(AstNode[] args) : IProjectMAction {
  public void execute(ProjectM model) {
    if (!args.check_os_flags()) {
      return;
    }
    if (!args.check_build_config(model.depo.build_config)) {
      return;
    }
    var dirs = args.unpack_as_string_array_skip_flags()
      .Select(x => Path.Join(model.depo.dir, x))
      .ToArray();
    var flags = args.parse_flags<VisibilityFlags>();
    model.include.Add(new Include(flags, dirs));
  }
}

internal class LinkAction(AstNode[] args) : IProjectMAction {
  public void execute(ProjectM model) {
    if (!args.check_os_flags()) {
      return;
    }
    if (!args.check_build_config(model.depo.build_config)) {
      return;
    }
    var libs       = args.unpack_as_string_array_skip_flags();
    var flags      = args.parse_flags<LinkFlags>();
    var visibility = args.parse_flags<VisibilityFlags>();
    model.link.Add(new Link(visibility, flags, libs));
  }
}

internal class LinkDirAction(AstNode[] args) : IProjectMAction {
  public void execute(ProjectM model) {
    if (!args.check_os_flags()) {
      return;
    }
    if (!args.check_build_config(model.depo.build_config)) {
      return;
    }
    var dirs = args.unpack_as_string_array_skip_flags()
      .Select(x => Path.Join(model.depo.dir, x))
      .ToArray();
    var visibility = args.parse_flags<VisibilityFlags>();
    model.link_dirs.Add(new LinkDir(visibility, dirs));
  }
}

internal class CFlagsAction(AstNode[] args) : IProjectMAction {
  public void execute(ProjectM model) {
    if (!args.check_os_flags()) {
      return;
    }
    if (!args.check_build_config(model.depo.build_config)) {
      return;
    }
    var values = args.unpack_as_string_array_skip_flags();
    var flags  = args.parse_flags<VisibilityFlags>();
    model.cflags.Add(new CFlags(flags, values));
  }
}

internal class ProjectAction : IDepoMAction {
  private readonly List<IProjectMAction> _actions = [];
  public           string                name;

  public ProjectAction(AstNode[] args) {
    name = args.First().as_leaf();
    foreach (var node in args.Skip(1)) {
      switch (node.value) {
        case "kind":  _actions.Add(new KindAction(node.children)); break;
        case "files": _actions.Add(new FilesAction(node.children)); break;
        case "ex-files": _actions.Add(new ExcludeFilesAction(node.children)); break;
        case "include":
        case "inc": _actions.Add(new IncludeAction(node.children)); break;
        case "link":      _actions.Add(new LinkAction(node.children)); break;
        case "link-dirs": _actions.Add(new LinkDirAction(node.children)); break;
        case "flags":     _actions.Add(new CFlagsAction(node.children)); break;
        default:          throw new Exception($"Unexpected node for project: {node.value}");
      }
    }
  }

  public void execute(DepoM model) {
    var project = new ProjectM { depo = model, name = name };
    foreach (var action in _actions) {
      action.execute(project);
    }
    model.projects.Add(project);
  }
}

internal class RequireAction(AstNode[] args) : IDepoMAction {
  public void execute(DepoM model) {
    model.require = args.unpack_as_string_array()
      .Select(path => Path.Join(model.dir, path))
      .ToArray();
  }
}

internal class TargetsAction(AstNode[] args) : IDepoMAction {
  public void execute(DepoM model) {
    model.targets = args.unpack_as_string_array();
  }
}

internal class DepsAction : IDepoMAction {
  private List<IDepoMAction> _actions = [];

  public DepsAction(AstNode[] args) {
    foreach (var node in args) {
      IDepoMAction action = node.value switch {
        "git"     => new GitAction(node.children),
        "svn"     => new SvnAction(node.children),
        "archive" => new ArchiveAction(node.children),
        _         => throw new Exception($"{node.value} is not a depo action"),
      };
      _actions.Add(action);
    }
  }

  public void execute(DepoM model) {
    foreach (var action in _actions) {
      action.execute(model);
    }
  }
}

internal abstract class DepActionBase(AstNode[] ast_args) : IDepoMAction {
  public void execute(DepoM model) {
    var args = ast_args.unpack_as_string_array_skip_flags();
    if (args.Length != 2 && args.Length != 3) {
      throw new Exception("Bad dependency arguments");
    }
    var dep = new DependencyM { name = args[0], url = args[1] };
    if (args.Length == 3) {
      dep.branch = args[2];
    }
    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
      dep.url = dep.url.Replace("{os}", "windows");
    } else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) {
      dep.url = dep.url.Replace("{os}", "linux");
    } else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) {
      dep.url = dep.url.Replace("{os}", "macos");
    }
    add_dep(model, dep);
  }

  protected abstract void add_dep(DepoM model, DependencyM dep);
}

internal class GitAction(AstNode[] args) : DepActionBase(args) {
  protected override void add_dep(DepoM model, DependencyM dep) {
    model.git_deps.Add(dep);
  }
}

internal class SvnAction(AstNode[] args) : DepActionBase(args) {
  protected override void add_dep(DepoM model, DependencyM dep) {
    model.svn_deps.Add(dep);
  }
}

internal class ArchiveAction(AstNode[] args) : DepActionBase(args) {
  protected override void add_dep(DepoM model, DependencyM dep) {
    model.archive_deps.Add(dep);
  }
}

internal class BinAction(AstNode[] args) : IDepoMAction {
  public void execute(DepoM model) {
    if (!args.check_os_flags()) {
      return;
    }
    if (!args.check_build_config(model.build_config)) {
      return;
    }
    foreach (var value in args.unpack_as_string_array_skip_flags()) {
      model.bin.Add(Path.Join(model.dir, value));
    }
  }
}

internal class CustomCommandAction(AstNode[] args) : IDepoMAction {
  public void execute(DepoM model) {
    if (!args.check_os_flags()) {
      Log.debug("Check os failed");
      return;
    }
    var str_args = args.unpack_as_string_array_skip_flags();
    if (str_args.Length < 2) {
      throw new Exception("Bad cmd arguments. Expected at least name and program to run");
    }
    var name     = str_args[0];
    var run_args = str_args.Skip(1).ToArray();
    model.custom_commands.Add(new CustomCommandM { name = name, args = run_args });
  }
}

internal class DepoAction {
  private readonly List<IDepoMAction> _actions = [];

  public DepoAction(AstNode[] args) {
    foreach (var arg in args) {
      IDepoMAction node = arg.value switch {
        "project" => new ProjectAction(arg.children),
        "require" => new RequireAction(arg.children),
        "targets" => new TargetsAction(arg.children),
        "deps"    => new DepsAction(arg.children),
        "bin"     => new BinAction(arg.children),
        "cmd"     => new CustomCommandAction(arg.children),
        _         => throw new Exception($"Unexpected node for depo: {arg.value}"),
      };
      _actions.Add(node);
    }
  }

  public DepoM call(string dir, BuildConfig config) {
    var model = new DepoM { dir = dir, build_config = config };
    foreach (var action in _actions) {
      action.execute(model);
    }
    return model;
  }
}
