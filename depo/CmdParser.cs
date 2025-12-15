namespace depo;

internal enum CmdAction {
  Build,
  Clean,
  Pull,
  Run,
}

internal sealed class CmdParser {
  public HashSet<CmdAction> actions = [];
  public BuildConfig config = BuildConfig.Debug;
  public string run_target = null;

  public CmdParser parse() {
    var args = Environment.GetCommandLineArgs().Skip(1).ToArray();
    var (flags, positional) = parse_verb(args);

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

    if (actions.Contains(CmdAction.Run)) {
      run_target = positional.Count != 0 ? positional[0] : null;
    }

    Log.info($"Config: {config}");
    Log.info($"Actions: {string.Join(',', actions)}");
    return this;
  }

  private (List<string> flags, List<string> positional) parse_verb(string[] args) {
    var flags = new List<string>();
    var positional = new List<string>();

    foreach (var arg in args) {
      if (arg.StartsWith('-')) {
        flags.Add(arg);
        continue;
      }

      if ("build".StartsWith(arg)) {
        actions.Add(CmdAction.Build);
      } else if ("clean".StartsWith(arg)) {
        actions.Add(CmdAction.Clean);
      } else if ("pull".StartsWith(arg)) {
        actions.Add(CmdAction.Pull);
      } else if ("run".StartsWith(arg)) {
        actions.Add(CmdAction.Build);
        actions.Add(CmdAction.Run);
      } else {
        positional.Add(arg);
      }
    }

    return (flags, positional);
  }
}