using System.Runtime.InteropServices;

namespace depo;

internal static class VsCode {
  internal static void generate(DepoM depo, BuildConfig cfg) {
    if (depo.targets.Length == 0) {
      return;
    }

    if (!Directory.Exists(".vscode")) {
      Directory.CreateDirectory(".vscode");
    }

    string target = depo.targets.First();

    var c_cpp_properties = $$"""
      {
        "version": 4,
        "configurations": [
          {
            "name": "{{target}}",
            "compileCommands": "${workspaceFolder}/compile_commands.json"
          }
        ]
      }
      """;
    File.WriteAllText(".vscode/c_cpp_properties.json", c_cpp_properties);

    var launch = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
      ? $$"""
      {
        "version": "0.2.0",
        "configurations": [
          {
            "name": "Debug {{target}}",
            "type": "cppvsdbg",
            "request": "launch",
            "program": "${workspaceFolder}/bin/{{cfg}}/{{target}}.exe",
            "args": [],
            "stopAtEntry": false,
            "cwd": "${workspaceFolder}",
            "environment": [],
            "console": "internalConsole",
            "internalConsoleOptions": "openOnSessionStart"
          }
        ]
      }
      """
      : $$"""
      {
        "version": "0.2.0",
        "configurations": [
          {
            "name": "Debug {{target}}",
            "type": "cppdbg",
            "request": "launch",
            "program": "${workspaceFolder}/bin/{{cfg}}/{{target}}",
            "args": [],
            "stopAtEntry": false,
            "cwd": "${workspaceFolder}",
            "environment": [],
            "MIMode": "lldb",
            //"preLaunchTask": "build-target",
            "console": "internalConsole",
            "internalConsoleOptions": "openOnSessionStart"
          }
        ]
      }
      """;

    File.WriteAllText(".vscode/launch.json", launch);
  }
}
