using System.Diagnostics;
using DepoBCS;

var timer = Stopwatch.StartNew();
var depo  = new DepoFile().parse();
var ninja = new Ninja(depo, BuildConfig.Debug);
ninja.generate();
Console.WriteLine($"Done! {timer.Elapsed}");
