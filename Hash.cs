using System.Security.Cryptography;
using System.Text;

namespace depo;

public static class Hash {
  private static byte[] get_file_hash(string file_path) {
    if (!File.Exists(file_path)) {
      return Encoding.UTF8.GetBytes(file_path);
    }
    using var stream = new BufferedStream(File.OpenRead(file_path), 1024 * 1024);
    using var sha256 = SHA256.Create();
    return sha256.ComputeHash(stream);
  }

  public static bool is_files_equal(string file1, string file2) {
    byte[] hash1 = get_file_hash(file1);
    byte[] hash2 = get_file_hash(file2);
    return hash1.SequenceEqual(hash2);
  }
}
