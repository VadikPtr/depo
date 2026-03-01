using System.Diagnostics;
using System.Runtime.InteropServices;

namespace depo;

public record SubprocessResult {
  public string command;
  public string stdout;
  public string stderr;
  public int    code;

  public SubprocessResult dump(bool trim = false) {
    if (trim) {
      foreach (var line in stdout.Split('\n')) {
        var trimmed = line.Trim();
        if (trimmed.Length == 0) {
          continue;
        }
        Console.WriteLine(trimmed);
      }
      foreach (var line in stderr.Split('\n')) {
        var trimmed = line.Trim();
        if (trimmed.Length == 0) {
          continue;
        }
        Console.Error.WriteLine(trimmed);
      }
      return this;
    }

    var trimmed_stdout = stdout.AsSpan().Trim();
    if (trimmed_stdout.Length != 0) {
      Console.Write(stdout);
    }
    var trimmed_stderr = stderr.AsSpan().Trim();
    if (trimmed_stderr.Length != 0) {
      Console.Error.Write(stderr);
    }
    return this;
  }

  public SubprocessResult check() {
    if (code != 0) {
      throw new Exception($"Run '{command}' failed with exit code {code}");
    }
    return this;
  }

  public SubprocessResult check(Predicate<int> assertion) {
    if (!assertion(code)) {
      throw new Exception($"Run '{command}' failed with exit code {code}");
    }
    return this;
  }
}

public static class Subprocess {
  private static Process _current_process;

  public static SubprocessResult run(params string[] command) {
    command = command.Where(x => !string.IsNullOrEmpty(x)).ToArray();
    using Process process     = new Process();
    var           file_name   = find_exe(command[0]);
    var           commandline = $"{file_name} {string.Join(' ', command.Skip(1))}";
    Log.debug("Running {0}", commandline);
    ProcessStartInfo info = new ProcessStartInfo {
      FileName               = file_name,
      RedirectStandardOutput = true,
      RedirectStandardError  = true,
      UseShellExecute        = false,
    };
    foreach (var arg in command.Skip(1)) {
      info.ArgumentList.Add(arg);
    }
    process.StartInfo = info;
    process.Start();
    var stdout = process.StandardOutput.ReadToEnd();
    var stderr = process.StandardError.ReadToEnd();
    _current_process = process;
    process.WaitForExit();
    _current_process = null;
    return new SubprocessResult {
      command = commandline,
      stdout  = stdout,
      stderr  = stderr,
      code    = process.ExitCode,
    };
  }

  public static void run_console_out(params string[] command) {
    command = command.Where(x => !string.IsNullOrEmpty(x)).ToArray();
    using Process process     = new Process();
    var           file_name   = find_exe(command[0]);
    var           commandline = $"{file_name} {string.Join(' ', command.Skip(1))}";
    Log.debug("Running {0}", commandline);
    ProcessStartInfo info = new ProcessStartInfo {
      FileName               = file_name,
      RedirectStandardOutput = true,
      RedirectStandardError  = true,
      UseShellExecute        = false,
      CreateNoWindow         = true,
    };
    foreach (var arg in command.Skip(1)) {
      info.ArgumentList.Add(arg);
    }
    process.StartInfo = info;
    process.OutputDataReceived += (_, e) => {
      if (e.Data != null) {
        Console.WriteLine(e.Data);
      }
    };
    process.ErrorDataReceived += (_, e) => {
      if (e.Data != null) {
        Console.Error.WriteLine(e.Data);
      }
    };
    process.Start();
    _current_process = process;
    process.BeginOutputReadLine();
    process.BeginErrorReadLine();
    process.WaitForExit();
    _current_process = null;
    if (process.ExitCode != 0) {
      throw new Exception($"Run '{commandline}' failed with exit code {process.ExitCode}");
    }
  }

  public static void kill_current() {
    if (_current_process is not { HasExited: false }) {
      return;
    }
    try {
      _current_process?.Kill(entireProcessTree: true);
    } catch (InvalidOperationException) {
      // ignore
    }
    _current_process = null;
  }

  private static string find_exe(string name) {
    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && !name.EndsWith(".exe")) {
      name += ".exe";
    }
    if (name.Contains('/') || name.Contains('\\')) {
      // not a cmd name, but path
      return name;
    }
    var path = Environment.GetEnvironmentVariable("PATH")
               ?? throw new Exception("Environment variable PATH is not set");
    foreach (var dir in path.Split(Path.PathSeparator)) {
      var try_path = Path.Join(dir, name);
      if (File.Exists(try_path)) {
        return try_path;
      }
    }
    throw new Exception($"Cannot find {name}");
  }
}
