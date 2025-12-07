using System.Runtime.InteropServices;

namespace depo;

internal class Dependencies {
  private readonly DepoM  _depo;
  private readonly string _deps_dir;

  public Dependencies(DepoM depo) {
    _depo     = depo;
    _deps_dir = create_deps_dir();
  }

  internal void pull() {
    if (_deps_dir == null) {
      Console.WriteLine("No deps!");
      return;
    }
    string cwd = Path.GetFullPath(Environment.CurrentDirectory);
    foreach (var dependency in _depo.git_deps) {
      pull_git(dependency);
    }
    foreach (var dependency in _depo.svn_deps) {
      pull_svn(dependency);
    }
    foreach (var dependency in _depo.archive_deps) {
      pull_archive(dependency);
    }
    Environment.CurrentDirectory = cwd;
  }

  private void pull_archive(DependencyM dependency) {
    Log.debug("archive pull {0}", dependency.name);
    string dir = Path.Join(_deps_dir, dependency.name);
    if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
      throw new NotImplementedException();
    }

    bool needs_fetch = check_ts_is_same(dir, dependency.url);
    if (!needs_fetch) {
      return;
    }

    if (Directory.Exists(dir)) {
      Directory.Delete(dir, recursive: true);
    }
    Directory.CreateDirectory(dir);
  }

  private void pull_svn(DependencyM dependency) {
    Log.debug("svn pull {0}", dependency.name);
    string dir = Path.Join(_deps_dir, dependency.name);
    if (!Directory.Exists(dir)) {
      Environment.CurrentDirectory = _deps_dir;
      Subprocess.run_console_out("svn", "co", dependency.url, dependency.name);
    }
    Environment.CurrentDirectory = dir;
    Subprocess.run_console_out("svn", "update");
  }

  private void pull_git(DependencyM dependency) {
    Log.debug("git pull {0}", dependency.name);
    string dir = Path.Join(_deps_dir, dependency.name);
    if (!Directory.Exists(dir)) {
      Environment.CurrentDirectory = _deps_dir;
      Subprocess.run_console_out("git", "clone", dependency.url, dependency.name);
    }
    Environment.CurrentDirectory = dir;
    Subprocess.run_console_out("git", "pull");
  }

  private string create_deps_dir() {
    if (!(_depo.archive_deps.Any() || _depo.git_deps.Any() || _depo.svn_deps.Any())) {
      return null;
    }
    string dir = Path.Join(Environment.CurrentDirectory, "deps");
    if (!Directory.Exists(dir)) {
      Directory.CreateDirectory(dir);
    }
    return Path.GetFullPath(dir);
  }

  private static bool check_ts_is_same(string dir, string url) {
    string ts_path = Path.Join(dir, "ts");
    if (!File.Exists(ts_path)) {
      return false;
    }
    string    local_ts = File.ReadAllText(ts_path);
    string    ts_url   = PathLib.replace_ext_full(url, ".ts"); // .tar.xz -> .ts
    using var cli      = new HttpClient();
    var       response = cli.GetAsync(ts_url).Result;
    if (!response.IsSuccessStatusCode) {
      Log.info("ts not found on server! {0}", Path.GetFileNameWithoutExtension(url));
      return false;
    }
    string remote_ts = response.Content.ReadAsStringAsync().Result;
    if (remote_ts != local_ts) {
      Log.info("Local and remote differs: {0} vs {1}", local_ts.Trim(), remote_ts.Trim());
      return false;
    }
    Log.info("Local and remote is same, skip");
    return true;
  }
}
