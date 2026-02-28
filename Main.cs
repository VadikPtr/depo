using System.Diagnostics;
using System.Runtime.InteropServices;
using depo;

#if !DEBUG
try {
#endif

Environment.SetEnvironmentVariable("CLICOLOR_FORCE", "1"); // force ninja to colored output
ConsoleHelper.enable_virtual_terminal();
Console.CancelKeyPress += (s, e) => { Subprocess.kill_current(); };

var timer = Stopwatch.StartNew();
var cmd   = new CmdParser().parse();

if (cmd.actions.Contains(CmdAction.Clean)) {
  PathLib.unlink("bin");
  PathLib.unlink("build");
  PathLib.unlink("compile_commands.json");
}

if (cmd.actions.Contains(CmdAction.Pull)) {
  var depo_deps = new DepoFile().parse();
  var deps      = new Dependencies(depo_deps);
  deps.pull();
}

var depo = new DepoFile().parse();

if (cmd.actions.Contains(CmdAction.Build)) {
  MsvcContext.enter();
  var ninja = new SolutionContext(depo, cmd.config);
  ninja.generate();
  ninja.dump_compile_commands();
  ninja.build();
  FileCopy.copy_binary_files(depo, ninja.bin_directory);
}

if (cmd.actions.Contains(CmdAction.VsCode)) {
  VsCode.generate(depo, cmd.config);
}

if (cmd.actions.Contains(CmdAction.Run)) {
  var target = cmd.run_target ?? depo.targets[0];
  var path   = Path.Join("bin", cmd.config.ToString(), target);
  if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
    path += ".exe";
  }
  string[] console_args = [path, .. cmd.run_target_args];
  Subprocess.run_console_out(console_args);
}

if (cmd.actions.Contains(CmdAction.Cmd)) {
  var command = depo.custom_commands.FirstOrDefault(x => x.name == cmd.run_target);
  if (command == null) {
    throw new Exception(
      $"Command not found: {cmd.run_target}. " +
      $"Available commands: {string.Join(',', depo.custom_commands.Select(y => y.name))}"
    );
  }
  string[] console_args = [.. command.args, .. cmd.run_target_args];
  Subprocess.run_console_out(console_args);
}

Log.info("Done! {0}", timer.Elapsed);

#if !DEBUG
} catch (Exception ex) {
  Console.WriteLine(ex.ToString());
  Environment.ExitCode = -1;
}
#endif
