using System.Diagnostics;
using System.Runtime.InteropServices;
using depo;

#if !DEBUG
try {
#endif

Environment.SetEnvironmentVariable("CLICOLOR_FORCE", "1"); // force ninja to colored output
MsvcContext.enter();
ConsoleHelper.enable_virtual_terminal();
Console.CancelKeyPress += (_, _) => Subprocess.kill_current();

CmdParser cmd = new CmdParser().parse();

if (cmd.actions.Contains(CmdAction.Clean)) {
  PathLib.unlink("bin");
  PathLib.unlink("build");
  PathLib.unlink("compile_commands.json");
}

if (cmd.actions.Contains(CmdAction.Pull)) {
  var depo_deps = new DepoFile().parse(cmd.config);
  var deps      = new Dependencies(depo_deps);
  deps.pull();
}

DepoM depo_m = new DepoFile().parse(cmd.config);

if (!cmd.watch) {
  do_main(cmd, depo_m);
} else {
  new ChangeWatcher(() => do_main(cmd, depo_m)).start();
}

#if !DEBUG
} catch (Exception ex) {
  Console.WriteLine(ex.ToString());
  Environment.ExitCode = -1;
}
#endif

void do_main(CmdParser cmd, DepoM depo_m) {
  var timer = Stopwatch.StartNew();

  if (cmd.actions.Contains(CmdAction.Build)) {
    var solution = new SolutionContext(depo_m, cmd.config, cmd.target);
    solution.generate_enums();
    solution.generate();
    solution.dump_compile_commands();
    solution.build();
    FileCopy.copy_binary_files(depo_m, solution.bin_directory);
  }

  if (cmd.actions.Contains(CmdAction.VsCode)) {
    VsCode.generate(depo_m, cmd.config);
  }

  if (cmd.actions.Contains(CmdAction.Run)) {
    var target = cmd.target ?? depo_m.targets[0];
    var path   = Path.Join("bin", cmd.config.ToString(), target);
    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
      path += ".exe";
    }
    string[] console_args = [path, .. cmd.run_target_args];
    Subprocess.run_console_out(console_args);
  }

  if (cmd.actions.Contains(CmdAction.Cmd)) {
    var command = depo_m.custom_commands.FirstOrDefault(x => x.name == cmd.target);
    if (command == null) {
      throw new Exception(
        $"Command not found: {cmd.target}. " +
        $"Available commands: {string.Join(',', depo_m.custom_commands.Select(y => y.name))}"
      );
    }
    string[] console_args = [.. command.args, .. cmd.run_target_args];
    Subprocess.run_console_out(console_args);
  }

  Log.info("Done! {0}", timer.Elapsed);
}
