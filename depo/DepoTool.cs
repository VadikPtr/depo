using System.Runtime.InteropServices;

namespace depo;

public static class DepoTool {
  public static string ninja   = path_to("ninja") ?? throw new Exception("Ninja not found");
  public static string sz      = path_to("7z") ?? throw new Exception("7z not found");
  public static string vswhere = path_to("vswhere");

  private static string path_to(string tool_name) {
    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
      tool_name += ".exe";
    } else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) {
      return find_in_path(tool_name);
    }
    return Path.Join(AppContext.BaseDirectory, "depo-tools", tool_name);
  }

  private static string find_in_path(string tool) {
    var path = Environment.GetEnvironmentVariable("PATH");
    if (path == null) {
      return null;
    }
    var dirs = path.Split(':');
    foreach (var dir in dirs) {
      var test = Path.Join(dir, tool);
      if (File.Exists(test)) {
        return test;
      }
    }
    return null;
  }
}

public static class DepoSettings {
  public static string config_dir = get_config_dir();
  public static string msvc_env   = Path.Join(config_dir, "msvc_env");

  private static string get_config_dir() {
    var settings = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
    var dir      = Path.Join(settings, "depo");
    Log.debug("Settings path: {0}", dir);
    if (!Directory.Exists(dir)) {
      Directory.CreateDirectory(dir);
    }
    return dir;
  }
}
