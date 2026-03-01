using System.Text.RegularExpressions;

namespace depo;

internal sealed record AstNode(string value, AstNode[] children) {
  internal bool is_leaf => children.Length == 0;

  internal string as_leaf() {
    if (!is_leaf) {
      throw new Exception($"Expected node {value} to be a leaf");
    }
    return value;
  }
}

internal sealed partial class AstParser(List<string> tokens) {
  private readonly Queue<string> _tokens = new(tokens);

  internal static AstNode parse(string text) {
    text = comments_regex().Replace(text, "");
    var parser = new AstParser(tokenize($"(depo {text})"));
    var expr   = parser.parse_expression();
    return expr;
  }

  private AstNode parse_expression() {
    var token = _tokens.Dequeue();
    if (token == ")") {
      throw new InvalidOperationException("Unbalanced parentheses!");
    }
    if (token != "(") {
      return new AstNode(token, []);
    }

    var head = parse_expression();
    var arguments = new List<AstNode>();
    while (_tokens.Peek() != ")") {
      arguments.Add(parse_expression());
    }
    _tokens.Dequeue();

    // Log.debug("{0}", expr);
    return new AstNode(head.value, arguments.ToArray());
  }

  private static List<string> tokenize(string code) {
    var          matches = split_regex().Matches(code.Trim());
    List<string> tokens  = [];
    foreach (Match match in matches) {
      var value = match.Value.Trim();
      if (value.Length != 0) {
        // Log.debug("Token: {0}", value);
        tokens.Add(value);
      }
    }
    return tokens;
  }

  [GeneratedRegex(@"\s*(,|[()]|[\w\[\]/':*\.\,\-_=@{}]+|[\S])")]
  private static partial Regex split_regex();

  [GeneratedRegex(@";.*", RegexOptions.Multiline)]
  private static partial Regex comments_regex();
}
