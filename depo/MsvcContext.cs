using System.Runtime.InteropServices;
using System.Text.Json;

namespace depo;

internal class MsvcProduct {
  public string displayName;
  public string installationPath;
}

public static class MsvcContext {
  public static void enter() {
    if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
      return;
    }
    if (Subprocess.try_find_exe("clang", out _)) {
      // clang present, no need to enter msvc env
      return;
    }
    if (Environment.GetEnvironmentVariable("VSCMD_VER") != null) {
      // already entered
      return;
    }
    if (!fetch_env()) {
      throw new Exception("Cannot get msvc env");
    }
    apply_env_from_file();
  }

  private static void apply_env_from_file() {
    var lines = File.ReadAllLines(DepoSettings.msvc_env);
    var dict  = new Dictionary<string, string>();

    foreach (var line in lines) {
      var split = line.Split('=', 2);
      if (split.Length != 2) {
        Log.error("Ignore invalid env line: {0}", line);
        continue;
      }
      var name  = split[0];
      var value = split[1];
      dict[name] = value;
    }

    var vs_install_dir = dict["VSINSTALLDIR"];
    if (vs_install_dir == null) {
      throw new Exception("VSINSTALLDIR not found");
    }

    foreach (var (key, value) in dict) {
      if (string.Equals(key, "PATH", StringComparison.InvariantCultureIgnoreCase)) {
        var values = value
          .Split(';')
          .Where(cur => cur.StartsWith(vs_install_dir, StringComparison.InvariantCultureIgnoreCase));
        var path = string.Join(';', values.Append(Environment.GetEnvironmentVariable("PATH") ?? ""));
        Log.debug("Set PATH: {0}", path);
        Environment.SetEnvironmentVariable("PATH", path);
        continue;
      }
      if (key.StartsWith("vscode", StringComparison.InvariantCultureIgnoreCase)) {
        continue;
      }
      if (key.StartsWith("windows", StringComparison.InvariantCultureIgnoreCase) ||
          key.StartsWith("VC") ||
          key.StartsWith("VS") ||
          key.StartsWith("Visual") ||
          key.StartsWith("LIB") ||
          key.StartsWith("INCLUDE")) {
        Log.debug("Set {0}: {1}", key, value);
        Environment.SetEnvironmentVariable(key, value);
      }
    }
  }

  private static bool fetch_env() {
    if (File.Exists(DepoSettings.msvc_env)) {
      return true;
    }
    var vs = find_vs_dev_cmd();
    Log.debug("VS: {0}", vs);
    if (vs == null) {
      Log.error("VS not found");
      return false;
    }

    using var tmp_file = new TemporaryFile(".bat");
    File.WriteAllText(tmp_file.path,
                      $"""
                      @echo off
                      setlocal
                      set VSDEVCMD_PATH="{vs}"
                      call %VSDEVCMD_PATH%
                      set > {DepoSettings.msvc_env}
                      endlocal
                      """);
    Subprocess.run_console_out("cmd", "/c", tmp_file.path);
    return File.Exists(DepoSettings.msvc_env);
  }

  private static string find_vs_dev_cmd() {
    var products_result = Subprocess.run(
        DepoTool.vswhere, "-products", "*", "-format", "json", "-utf8", "-requires",
        "Microsoft.VisualStudio.Component.VC.Tools.x86.x64", "-prerelease"
      )
      .check()
      .dump();
    var products = JsonSerializer.Deserialize(products_result.stdout, TheJsonContext.Default.MsvcProductArray);
    foreach (var product in products) {
      Log.debug("Found VS: {0} {1}", product.displayName, product.installationPath);
      var installation_path = product.installationPath;
      if (installation_path == null) {
        continue;
      }
      string[] try_paths = [
        Path.Join(installation_path, "VC", "Auxiliary", "Build", "vcvars64.bat"),
        // Path.Join(installation_path, "VC", "Auxiliary", "Build", "vcvars32.bat"),
        Path.Join(installation_path, "Common7", "Tools", "vsvars64.bat"),
        // Path.Join(installation_path, "Common7", "Tools", "vsvars32.bat"),
        Path.Join(installation_path, "Common7", "Tools", "vsdevcmd.bat"),
      ];
      foreach (var try_path in try_paths) {
        if (File.Exists(try_path)) {
          return try_path;
        }
      }
    }
    return null;
  }
}
