using System.Text.RegularExpressions;

namespace depo;

internal sealed partial class Parser(List<string> tokens) {
  private readonly Queue<string> _tokens = new(tokens);

  internal static DepoAction parse(string text) {
    text = comments_regex().Replace(text, "");
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
     Console.WriteLine(expr);
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
      "deps"    => new DepsAction(args),
      "git"     => new GitAction(args),
      "svn"     => new SvnAction(args),
      "archive" => new ArchiveAction(args),
      _         => throw new Exception($"{name} call is unknown"),
    };
  }

  private static List<string> tokenize(string code) {
    var          matches = split_regex().Matches(code.Trim());
    List<string> tokens  = [];
    foreach (Match match in matches) {
      var value = match.Value.Trim();
      if (value.Length != 0) {
        //Console.WriteLine($"Token: {value}");
        tokens.Add(value);
      }
    }
    return tokens;
  }

  [GeneratedRegex(@"\s*(,|[()]|[\w\[\]/':*\.\-_=@]+|[\S])")]
  private static partial Regex split_regex();

  [GeneratedRegex(@";.*", RegexOptions.Multiline)]
  private static partial Regex comments_regex();
}