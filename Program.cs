using System.Diagnostics;
using depo;

var timer = Stopwatch.StartNew();
var cmd   = new CmdParser().parse();
var depo  = new DepoFile().parse();

if (cmd.actions.Contains(CmdAction.Build)) {
  var ninja = new SolutionContext(depo, cmd.config);
  ninja.generate();
  ninja.dump_compile_commands();
  ninja.build();
  FileCopy.copy_binary_files(depo, ninja.bin_directory);
}

Console.WriteLine($"Done! {timer.Elapsed}");
