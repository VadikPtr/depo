using System.Net;
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
    Log.info("--- Archive pull {0}", dependency.name);
    string dir = Path.Join(_deps_dir, dependency.name);

    bool is_same = check_ts_is_same(dir, dependency.url);
    if (is_same) {
      Log.debug("No need to fetch");
      return;
    }

    if (Directory.Exists(dir)) {
      Directory.Delete(dir, recursive: true);
    }
    Directory.CreateDirectory(dir);

    var archive_path = Path.Join(dir, Path.GetFileName(dependency.url));
    Log.debug("Archive path: {0}", archive_path);
    download(dependency.url, archive_path);
    unpack_archive(archive_path);
  }

  private static void download(string from, string to) {
    Log.info("Downloading: {0} -> {1}", from, to);
    using var client   = new HttpClient();
    using var response = client.GetAsync(from, HttpCompletionOption.ResponseHeadersRead).Result;
    response.EnsureSuccessStatusCode();
    using var stream = response.Content.ReadAsStream();
    using var file_stream = new FileStream(to, FileMode.Create, FileAccess.Write, FileShare.None,
                                           bufferSize: 8192, useAsync: false);
    stream.CopyTo(file_stream);
  }

  private static void unpack_archive(string archive_path) {
    Log.info("Unpack archive: {0}", archive_path);
    var dir = Path.GetDirectoryName(archive_path);

    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
      var sz = DepoTool.path_to("7z");
      Subprocess.run(sz, "x", "-o" + dir, archive_path).check();
      var tar_path = archive_path.Replace(".xz", "");
      Subprocess.run(sz, "x", "-o" + dir, tar_path).check();
      File.Delete(tar_path);
    } else {
      Subprocess.run("tar", "xf", archive_path, "-C", dir).check();
    }
    File.Delete(archive_path);
  }

  private void pull_svn(DependencyM dependency) {
    Log.info("--- Svn pull {0}", dependency.name);
    string dir = Path.Join(_deps_dir, dependency.name);
    if (!Directory.Exists(dir)) {
      Environment.CurrentDirectory = _deps_dir;
      Subprocess.run_console_out("svn", "co", dependency.url, dependency.name);
    }
    Environment.CurrentDirectory = dir;
    Subprocess.run_console_out("svn", "update");
  }

  private void pull_git(DependencyM dependency) {
    Log.info("--- Git pull {0}", dependency.name);
    string dir = Path.Join(_deps_dir, dependency.name);
    if (!Directory.Exists(dir)) {
      Environment.CurrentDirectory = _deps_dir;
      Subprocess.run_console_out("git", "clone", dependency.url, dependency.name);
    }
    Environment.CurrentDirectory = dir;
    Subprocess.run_console_out("git", "pull");
  }

  private string create_deps_dir() {
    if (_depo.archive_deps.Count == 0 &&
        _depo.git_deps.Count == 0 &&
        _depo.svn_deps.Count == 0) {
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
    using var response = cli.GetAsync(ts_url).Result;
    if (!response.IsSuccessStatusCode) {
      Log.info("TS not found on server! {0}", Path.GetFileNameWithoutExtension(url));
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
