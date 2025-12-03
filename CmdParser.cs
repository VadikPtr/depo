namespace DepoBCS;

internal enum CmdAction {
  Build,
  Clean,
  Pull,
}

internal sealed class CmdParser {
  public HashSet<CmdAction> actions = [];
  public BuildConfig        config  = BuildConfig.Debug;

  public CmdParser parse() {
    var args = Environment.GetCommandLineArgs().Skip(1).ToArray();
    args = parse_verb(args);
    if (actions.Count == 0) {
      actions.Add(CmdAction.Pull);
      actions.Add(CmdAction.Clean);
      actions.Add(CmdAction.Build);
    }
    foreach (var arg in args) {
      if (arg.StartsWith("-r")) {
        config = BuildConfig.Release;
      } else if (arg.StartsWith("-d")) {
        config = BuildConfig.Debug;
      } else {
        throw new Exception($"Unknown argument: {arg}");
      }
    }

    Console.WriteLine($"Config: {config}");
    Console.WriteLine($"Actions: {string.Join(',', actions)}");
    return this;
  }

  private string[] parse_verb(string[] args) {
    foreach (var arg in args) {
      if (arg.StartsWith('-')) {
        continue;
      }

      if ("build".StartsWith(arg)) {
        actions.Add(CmdAction.Build);
      } else if ("clean".StartsWith(arg)) {
        actions.Add(CmdAction.Clean);
      } else if ("pull".StartsWith(arg)) {
        actions.Add(CmdAction.Pull);
      } else {
        throw new Exception($"Unknown verb: {arg}");
      }

      return args.Where(x => x != arg).ToArray();
    }
    return args;
  }
}
