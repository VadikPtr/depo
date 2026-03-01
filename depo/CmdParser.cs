namespace depo;

internal enum CmdAction {
  Build,
  Clean,
  Pull,
  Run,
  Cmd,
  VsCode,
}

internal sealed class CmdParser {
  public readonly HashSet<CmdAction> actions = [];
  public          BuildConfig        config  = BuildConfig.Debug;
  public          bool               watch   = false;
  public          string             target;
  public          string[]           run_target_args = [];

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
      } else if (flag.StartsWith("-w")) {
        watch = true;
      } else {
        throw new Exception($"Unknown argument: {flag}");
      }
    }

    if (actions.Contains(CmdAction.Run) ||
        actions.Contains(CmdAction.Build) ||
        actions.Contains(CmdAction.Cmd)) {
      target          = positional.Count != 0 ? positional[0] : null;
      run_target_args = positional.Skip(1).ToArray();
    }

    Log.info($"Config: {config}");
    Log.info($"Actions: {string.Join(',', actions)}");
    return this;
  }

  private (List<string> flags, List<string> positional) parse_verb(string[] args) {
    var  flags             = new List<string>();
    var  positional        = new List<string>();
    bool record_positional = false;

    foreach (var arg in args) {
      if (arg == "--") {
        record_positional = true;
        continue;
      }

      if (arg.StartsWith('-')) {
        if (record_positional) {
          positional.Add(arg);
        } else {
          flags.Add(arg);
        }
        continue;
      }

      if ("build".StartsWith(arg)) {
        actions.Add(CmdAction.Build);
      } else if ("clean".StartsWith(arg)) {
        actions.Add(CmdAction.Clean);
      } else if ("cmd".StartsWith(arg)) {
        actions.Add(CmdAction.Cmd);
      } else if ("pull".StartsWith(arg)) {
        actions.Add(CmdAction.Pull);
      } else if ("vscode".StartsWith(arg)) {
        actions.Add(CmdAction.VsCode);
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
