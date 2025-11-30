namespace DepoBCS;

internal static class NinjaExt {
  public static string path_escape_ninja(this string path) {
    return path.Replace(":", "$:");
  }
}
