using System.Runtime.InteropServices;

namespace depo;

public static class DepoTool {
  public static string path_to(string tool_name) {
    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
      tool_name += ".exe";
    }
    return Path.Join(AppContext.BaseDirectory, "depo-tools", tool_name);
  }
}
