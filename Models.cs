using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace DepoBCS;

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

internal class DepoM {
  public string         dir;
  public string[]       require  = [];
  public string[]       targets  = [];
  public List<string>   bin      = [];
  public List<ProjectM> projects = [];
}

internal interface IExpr;

internal class ExprValue : IExpr {
  internal string value { get; }

  internal ExprValue(string value) {
    this.value = value;
  }

  public override string ToString() {
    return $"\"{value}\"";
  }
}

// internal class ExprCall(string name, List<IExpr> args) : IExpr {
//   public override string ToString() {
//     return $"(call:{name} {string.Join(", ", args.Select(x => x.ToString()))})";
//   }
// }

internal static class ExprExt {
  internal static string[] unpack_as_string_array(this List<IExpr> exprs) {
    string[] result = new string[exprs.Count];
    for (int i = 0; i < exprs.Count; i++) {
      if (exprs[i] is not ExprValue value) {
        throw new InvalidOperationException($"Expected {exprs[i]} to be value expression!");
      }
      result[i] = value.value;
    }
    return result;
  }

  internal static string[] unpack_as_string_array_skip_flags(this List<IExpr> exprs) {
    return unpack_as_string_array(exprs).Where(x => !x.StartsWith('\'')).ToArray();
  }

  internal static string unpack_as_string(this List<IExpr> exprs) {
    if (exprs.Count == 0) {
      throw new InvalidOperationException("Expected to have at least one expression!");
    }
    if (exprs.Count > 1) {
      throw new InvalidOperationException("Expected to have at most one expression!");
    }
    if (exprs[0] is not ExprValue value) {
      throw new InvalidOperationException($"Expected {exprs[0]} to be value expression!");
    }
    return value.value;
  }

  internal static TEnum parse_flags<TEnum>(this List<IExpr> exprs)
    where TEnum : struct, Enum {
    Debug.Assert(Enum.GetUnderlyingType(typeof(TEnum)) == typeof(uint));
    Unsafe.SkipInit(out TEnum flags);
    ref uint flags_int = ref Unsafe.As<TEnum, uint>(ref flags);
    foreach (var expr in exprs) {
      if (expr is not ExprValue value_expr) {
        continue;
      }
      if (!value_expr.value.StartsWith('\'')) {
        continue;
      }
      ReadOnlySpan<char> str = value_expr.value;
      str = str[1..];
      if (!Enum.TryParse<TEnum>(str, ignoreCase: true, out var value)) {
        // Console.WriteLine($"Can't parse {str} as {typeof(TEnum).Name}");
        continue;
      }
      flags_int |= Unsafe.As<TEnum, uint>(ref value);
    }
    return flags;
  }

  internal static bool check_os_flags(this List<IExpr> exprs) {
    OS os = exprs.parse_flags<OS>();
    if (os == OS.None) {
      return true;
    }
    if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) {
      return (os & OS.Mac) != 0;
    }
    if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) {
      return (os & OS.Lin) != 0;
    }
    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
      return (os & OS.Win) != 0;
    }
    throw new Exception($"OS unhandled: {os}");
  }
}

internal interface IDepoMAction : IExpr {
  void execute(DepoM model);
}

internal interface IProjectMAction : IExpr {
  void execute(ProjectM model);
}

internal class KindAction(List<IExpr> expr_args) : IProjectMAction {
  public void execute(ProjectM model) {
    var arg = expr_args.unpack_as_string();
    if (!Enum.TryParse(arg, ignoreCase: true, out Kind kind)) {
      throw new InvalidOperationException($"Invalid kind value: {arg}!");
    }
    model.kind = kind;
  }
}

internal class FilesAction(List<IExpr> expr_args) : IProjectMAction {
  public void execute(ProjectM model) {
    if (!expr_args.check_os_flags()) {
      return;
    }
    var files = expr_args.unpack_as_string_array_skip_flags();
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

internal class IncludeAction(List<IExpr> expr_args) : IProjectMAction {
  public void execute(ProjectM model) {
    if (!expr_args.check_os_flags()) {
      return;
    }
    var dirs = expr_args.unpack_as_string_array_skip_flags()
      .Select(x => Path.Join(model.depo.dir, x))
      .ToArray();
    var flags = expr_args.parse_flags<VisibilityFlags>();
    model.include.Add(new Include(flags, dirs));
  }
}

internal class LinkAction(List<IExpr> expr_args) : IProjectMAction {
  public void execute(ProjectM model) {
    if (!expr_args.check_os_flags()) {
      return;
    }
    var libs       = expr_args.unpack_as_string_array_skip_flags();
    var flags      = expr_args.parse_flags<LinkFlags>();
    var visibility = expr_args.parse_flags<VisibilityFlags>();
    model.link.Add(new Link(visibility, flags, libs));
  }
}

internal class CFlagsAction(List<IExpr> expr_args) : IProjectMAction {
  public void execute(ProjectM model) {
    if (!expr_args.check_os_flags()) {
      return;
    }
    var values = expr_args.unpack_as_string_array_skip_flags();
    var flags  = expr_args.parse_flags<VisibilityFlags>();
    model.cflags.Add(new CFlags(flags, values));
  }
}

internal class ProjectAction(List<IExpr> expr_args) : IDepoMAction {
  public void execute(DepoM model) {
    var name    = (ExprValue)expr_args.First();
    var project = new ProjectM { depo = model, name = name.value };
    foreach (var expr in expr_args.Skip(1)) {
      if (expr is IProjectMAction action) {
        action.execute(project);
      } else {
        Console.WriteLine($"Not handled: {expr}");
      }
    }
    model.projects.Add(project);
  }
}

internal class RequireAction(List<IExpr> expr_args) : IDepoMAction {
  public void execute(DepoM model) {
    model.require = expr_args.unpack_as_string_array()
      .Select(path => Path.Join(model.dir, path))
      .ToArray();
  }
}

internal class TargetsAction(List<IExpr> expr_args) : IDepoMAction {
  public void execute(DepoM model) {
    model.targets = expr_args.unpack_as_string_array();
  }
}

internal class BinAction(List<IExpr> expr_args) : IDepoMAction {
  public void execute(DepoM model) {
    if (!expr_args.check_os_flags()) {
      return;
    }
    foreach (var value in expr_args.unpack_as_string_array_skip_flags()) {
      model.bin.Add(Path.Join(model.dir, value));
    }
  }
}

internal class DepoAction(List<IExpr> expr_args) : IExpr {
  public DepoM call(string dir) {
    var model = new DepoM { dir = dir };
    foreach (var expr in expr_args) {
      if (expr is IDepoMAction action) {
        action.execute(model);
      }
    }
    return model;
  }
}

internal sealed partial class Parser(List<string> tokens) {
  private readonly Queue<string> _tokens = new(tokens);

  internal static DepoAction parse(string text) {
    var parser = new Parser(tokenize($"(depo {text})"));
    var expr   = parser.parse_expression();
    return expr as DepoAction ?? throw new Exception("Can't match default depo expression!");
  }

  private IExpr parse_expression() {
    var token = _tokens.Dequeue();
    if (token == ")") {
      throw new InvalidOperationException("Unbalanced parentheses!");
    }
    if (token != "(") {
      return new ExprValue(token);
    }

    if (parse_expression() is not ExprValue head) {
      throw new InvalidOperationException("Can't run function with function argument!");
    }
    var arguments = new List<IExpr>();
    while (_tokens.Peek() != ")") {
      arguments.Add(parse_expression());
    }
    _tokens.Dequeue();

    var expr = call_expression(head.value, arguments);
    // Console.WriteLine(expr);
    return expr;
  }

  private static IExpr call_expression(string name, List<IExpr> args) {
    return name switch {
      "kind"    => new KindAction(args),
      "files"   => new FilesAction(args),
      "include" => new IncludeAction(args),
      "link"    => new LinkAction(args),
      "project" => new ProjectAction(args),
      "depo"    => new DepoAction(args),
      "require" => new RequireAction(args),
      "targets" => new TargetsAction(args),
      "flags"   => new CFlagsAction(args),
      "bin"     => new BinAction(args),
      _         => throw new Exception($"{name} call is unknown"), // new ExprCall(name, args),
    };
  }

  private static List<string> tokenize(string code) {
    var          matches = split_regex().Matches(code.Trim());
    List<string> tokens  = [];
    foreach (Match match in matches) {
      var value = match.Value.Trim();
      if (value.Length != 0) {
        // Console.WriteLine($"Token: {value}");
        tokens.Add(value);
      }
    }
    return tokens;
  }

  [GeneratedRegex(@"\s*(,@|[()]|[\w\[\]/':*\.\-_]+|[\S])")]
  private static partial Regex split_regex();
}
