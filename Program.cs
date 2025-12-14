using System.Diagnostics;
using depo;

#if !DEBUG
try {
#endif

var timer = Stopwatch.StartNew();
var cmd = new CmdParser().parse();

if (cmd.actions.Contains(CmdAction.Clean)) {
  PathLib.unlink("bin");
  PathLib.unlink("build");
  PathLib.unlink("compile_commands.json");
}

if (cmd.actions.Contains(CmdAction.Pull)) {
  var depo = new DepoFile().parse();
  var deps = new Dependencies(depo);
  deps.pull();
}

if (cmd.actions.Contains(CmdAction.Build)) {
  var depo = new DepoFile().parse();
  var ninja = new SolutionContext(depo, cmd.config);
  ninja.generate();
  ninja.dump_compile_commands();
  ninja.build();
  FileCopy.copy_binary_files(depo, ninja.bin_directory);
}

Log.info("Done! {0}", timer.Elapsed);

#if !DEBUG
} catch (Exception ex) {
  Console.WriteLine(ex.ToString());
  Environment.ExitCode = -1;
}
#endif
