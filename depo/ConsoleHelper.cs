using System.Runtime.InteropServices;

namespace depo;

internal static class ConsoleHelper {
  private const int  STD_OUTPUT_HANDLE                  = -11;
  private const uint ENABLE_VIRTUAL_TERMINAL_PROCESSING = 0x0004;

  [DllImport("kernel32.dll", SetLastError = true)]
  static extern IntPtr GetStdHandle(int nStdHandle);

  [DllImport("kernel32.dll")]
  static extern bool GetConsoleMode(IntPtr hConsoleHandle, out uint lpMode);

  [DllImport("kernel32.dll")]
  static extern bool SetConsoleMode(IntPtr hConsoleHandle, uint dwMode);

  // NOTE: this helps with colored output
  public static void enable_virtual_terminal() {
    if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
      return;
    }

    var handle = GetStdHandle(STD_OUTPUT_HANDLE);
    if (GetConsoleMode(handle, out uint mode)) {
      SetConsoleMode(handle, mode | ENABLE_VIRTUAL_TERMINAL_PROCESSING);
    }
  }
}
