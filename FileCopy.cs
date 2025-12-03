namespace DepoBCS;

internal static class FileCopy {
  internal static void copy_binary_files(DepoM model, string out_dir) {
    Dictionary<string, List<string>> src_dirs_files = [];

    foreach (var bin in model.bin) {
      var dir  = PupokPath.parent(bin);
      var name = Path.GetFileName(bin);
      if (src_dirs_files.TryGetValue(dir, out List<string> src_dirs)) {
        src_dirs.Add(name);
      } else {
        src_dirs_files.Add(dir, [name]);
      }
    }

    foreach (var (dir, files) in src_dirs_files) {
      IEnumerable<string> ob = ["robocopy", dir, out_dir];
      string[] args = ob.Concat(files)
        .Concat(["/im", "/njs", "/njh", "/ndl", "/ts", "/np"])
        .ToArray();
      Subprocess.run(args).check(code => code < 8).dump(trim: true);
    }
  }
}
