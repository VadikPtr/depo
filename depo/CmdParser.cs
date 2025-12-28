namespace depo;

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
    var flags = parse_verb(args);

    if (actions.Count == 0) {
      actions.Add(CmdAction.Pull);
      actions.Add(CmdAction.Clean);
      actions.Add(CmdAction.Build);
    }

    foreach (var flag in flags) {
      if (flag.StartsWith("-r")) {
        config = BuildConfig.Release;
      } else if (flag.StartsWith("-d")) {
        config = BuildConfig.Debug;
      } else if (flag.StartsWith("-v")) {
        Log.is_debug = true;
      } else {
        throw new Exception($"Unknown argument: {flag}");
      }
    }

    Log.info($"Config: {config}");
    Log.info($"Actions: {string.Join(',', actions)}");
    return this;
  }

  private string[] parse_verb(string[] args) {
    var other = new List<string>();
    foreach (var arg in args) {
      if (arg.StartsWith('-')) {
        other.Add(arg);
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
    }
    return other.ToArray();
  }
}