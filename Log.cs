namespace depo;

internal static class Log {
  private const bool is_debug = true;
  
  internal static void debug(string message, params object[] args) {
    if (is_debug) {
      Console.WriteLine(message, args);
    }
  }

  internal static void info(string message, params object[] args) {
    Console.WriteLine(message, args);
  }
}