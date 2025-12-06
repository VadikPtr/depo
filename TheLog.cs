using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;

internal static class TheLog {
  static ILoggerFactory factory_;
  public static ILogger log { get; }
  
  static TheLog() {
    factory_ = LoggerFactory.Create(builder => {
      builder.AddSimpleConsole((SimpleConsoleFormatterOptions options) => {
        options.SingleLine = true;
        options.IncludeScopes = false;
        options.TimestampFormat = "HH:mm:ss ";
      });
    });
    log = factory_.CreateLogger("depo");
  }

  internal static void destroy()  {
    factory_?.Dispose();
  }
}