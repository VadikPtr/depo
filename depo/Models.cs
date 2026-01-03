using System.Runtime.InteropServices;
using System.Text.Json.Serialization;

namespace depo;

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

internal class ProjectM {
  [JsonIgnore] public DepoM         depo;
  public              string        name;
  public              Kind          kind;
  public              List<string>  files   = [];
  public              List<Include> include = [];
  public              List<Link>    link    = [];
  public              List<CFlags>  cflags  = [];
}

internal class DependencyM {
  public string name;
  public string url;
  public string branch;
}

internal class DepoM {
  public string            dir;
  public string[]          require      = [];
  public string[]          targets      = [];
  public List<string>      bin          = [];
  public List<ProjectM>    projects     = [];
  public List<DependencyM> git_deps     = [];
  public List<DependencyM> svn_deps     = [];
  public List<DependencyM> archive_deps = [];
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
    var files = args.unpack_as_string_array_skip_flags();
    foreach (var file in files) {
      if (file.Contains('*')) {
        foreach (var f in Directory.EnumerateFiles(model.depo.dir, file, SearchOption.AllDirectories)) {
          model.files.Add(f);
        }
      } else {
        var full_path = Path.Join(model.depo.dir, file);
        model.files.Add(full_path);
      }
    }
  }
}

internal class IncludeAction(AstNode[] args) : IProjectMAction {
  public void execute(ProjectM model) {
    if (!args.check_os_flags()) {
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
    var libs       = args.unpack_as_string_array_skip_flags();
    var flags      = args.parse_flags<LinkFlags>();
    var visibility = args.parse_flags<VisibilityFlags>();
    model.link.Add(new Link(visibility, flags, libs));
  }
}

internal class CFlagsAction(AstNode[] args) : IProjectMAction {
  public void execute(ProjectM model) {
    if (!args.check_os_flags()) {
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
        case "include":
        case "inc": _actions.Add(new IncludeAction(node.children)); break;
        case "link":  _actions.Add(new LinkAction(node.children)); break;
        case "flags": _actions.Add(new CFlagsAction(node.children)); break;
        default:      throw new Exception($"Unexpected node for project: {node.value}");
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
    foreach (var value in args.unpack_as_string_array_skip_flags()) {
      model.bin.Add(Path.Join(model.dir, value));
    }
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
        _         => throw new Exception($"Unexpected node for depo: {arg.value}"),
      };
      _actions.Add(node);
    }
  }

  public DepoM call(string dir) {
    var model = new DepoM { dir = dir };
    foreach (var action in _actions) {
      action.execute(model);
    }
    return model;
  }
}
