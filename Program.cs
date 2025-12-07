using System.Diagnostics;
using depo;

#if !DEBUG
try {
#endif

var timer = Stopwatch.StartNew();
var cmd   = new CmdParser().parse();
var depo  = new DepoFile().parse();

if (cmd.actions.Contains(CmdAction.Pull)) {
  var deps = new Dependencies(depo);
  deps.pull();
}

if (cmd.actions.Contains(CmdAction.Build)) {
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
}
#endif
