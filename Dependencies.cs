namespace depo;

internal class Dependencies {
  private DepoM  depo_;
  private string deps_dir_ = null;

  public Dependencies(DepoM depo) {
    depo_     = depo;
    deps_dir_ = create_deps_dir();
  }

  internal void pull() {
    if (deps_dir_ == null) {
      Console.WriteLine("No deps!");
      return;
    }
    string cwd = Path.GetFullPath(Environment.CurrentDirectory);
    pull_git();
    pull_svn();
    pull_archive();
    Environment.CurrentDirectory = cwd;
  }

  private void pull_archive() {
    foreach (var dependency in depo_.archive_deps) {
      throw new NotImplementedException();
    }
  }

  private void pull_svn() {
    foreach (var dependency in depo_.svn_deps) {
      throw new NotImplementedException();
    }
  }

  private void pull_git() {
    foreach (var dependency in depo_.git_deps) {
      string dir = Path.Join(deps_dir_, dependency.name);
      if (!Directory.Exists(dir)) {
        Environment.CurrentDirectory = deps_dir_;
        Subprocess.run_console_out("git", "clone", dependency.url, dependency.name);
      }
      Environment.CurrentDirectory = dir;
      Subprocess.run_console_out("git", "pull");
    }
  }

  private string create_deps_dir() {
    if (!(depo_.archive_deps.Any() || depo_.git_deps.Any() || depo_.svn_deps.Any())) {
      return null;
    }
    string dir = Path.Join(Environment.CurrentDirectory, "deps");
    if (!Directory.Exists(dir)) {
      Directory.CreateDirectory(dir);
    }
    return Path.GetFullPath(dir);
  }
}