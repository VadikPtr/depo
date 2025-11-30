using System.Diagnostics;
using DepoBCS;

var timer = Stopwatch.StartNew();
var depo  = new DepoFile().parse();
var ninja = new Ninja(depo, BuildConfig.Debug);
ninja.generate();
ninja.dump_compile_commands();
ninja.build();
FileCopy.copy_binary_files(depo, ninja.bin_directory);
Console.WriteLine($"Done! {timer.Elapsed}");
