using System.Runtime.InteropServices;

namespace depo;

internal static class FileCopy {
  internal static void copy_binary_files(DepoM model, string out_dir) {
    Dictionary<string, List<string>> src_dirs_files = [];

    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
      foreach (var bin in model.bin) {
        var dir = PathLib.parent(bin);
        var name = Path.GetFileName(bin);
        if (src_dirs_files.TryGetValue(dir, out List<string> src_dirs)) {
          src_dirs.Add(name);
        } else {
          src_dirs_files.Add(dir, [name]);
        }
      }

      foreach (var (dir, files) in src_dirs_files) {
        string[] args = ["robocopy", dir, out_dir, .. files, "/im", "/njs", "/njh", "/ndl", "/ts", "/np"];
        Subprocess.run(args).check(code => code < 8).dump(trim: true);
      }
    } else {
      foreach (var bin in model.bin) {
        var out_path = Path.Join(out_dir, Path.GetFileName(bin));
        if (!Hash.is_files_equal(bin, out_path)) {
          string[] args = ["cp", bin, out_dir];
          Subprocess.run(args).check().dump();
        }
      }
    }
  }
}
