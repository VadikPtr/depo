namespace depo;

internal static class Log {
  public static bool is_debug = false;

  internal static void debug(string message, params object[] args) {
    if (is_debug) {
      Console.WriteLine(message, args);
    }
  }

  internal static void info(string message, params object[] args) {
    Console.WriteLine(message, args);
  }

  internal static void error(string message, params object[] args) {
    Console.Error.WriteLine(message, args);
  }
}
