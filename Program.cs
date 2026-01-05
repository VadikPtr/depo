using System.Diagnostics;
using System.Runtime.InteropServices;
using depo;

#if !DEBUG
try {
#endif

var timer = Stopwatch.StartNew();
var cmd   = new CmdParser().parse();

if (cmd.actions.Contains(CmdAction.Clean)) {
  PathLib.unlink("bin");
  PathLib.unlink("build");
  PathLib.unlink("compile_commands.json");
}

if (cmd.actions.Contains(CmdAction.Pull)) {
  var depo_deps = new DepoFile().parse();
  var deps = new Dependencies(depo_deps);
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

if (cmd.actions.Contains(CmdAction.Run)) {
  var target = cmd.run_target ?? depo.targets[0];
  var path = Path.Join("bin", cmd.config.ToString(), target);
  if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
    path += ".exe";
  }
  string[] console_args = [path, .. cmd.run_target_args];
  Subprocess.run_console_out(console_args);
}

Log.info("Done! {0}", timer.Elapsed);

#if !DEBUG
} catch (Exception ex) {
  Console.WriteLine(ex.ToString());
  Environment.ExitCode = -1;
}
#endif
