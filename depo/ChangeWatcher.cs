namespace depo;

public class ChangeWatcher(Action action) {
  private static readonly TimeSpan   debounce_time = TimeSpan.FromMilliseconds(800);
  private                 DateTime   _last_restart = DateTime.UtcNow;
  private readonly        AtomicBool _is_exiting   = new(false);
  private readonly        AtomicBool _run_action   = new(true);

  public void start() {
    List<FileSystemWatcher> watchers   = [];
    string[]                extensions = ["*.hpp", "*.cpp", "*.h", "*.c", "*.m", "*.mm"];

    foreach (var ext in extensions) {
      var watcher = new FileSystemWatcher {
        Path                  = Environment.CurrentDirectory,
        NotifyFilter          = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName,
        Filter                = ext,
        IncludeSubdirectories = true,
        EnableRaisingEvents   = true,
      };
      watcher.Changed += (_, _) => restart_action(use_debounce: true);
      watcher.Created += (_, _) => restart_action(use_debounce: true);
      watcher.Renamed += (_, _) => restart_action(use_debounce: true);
      watcher.Deleted += (_, _) => restart_action(use_debounce: true);
      watchers.Add(watcher);
    }

    var thread = new Thread(thread_function);
    thread.Start();

    while (true) {
      Log.info("[r - reload, q - quit]");

      var line = Console.ReadLine();
      if (string.IsNullOrEmpty(line)) {
        continue;
      }
      if (line.Trim() == "q") {
        break;
      }
      if (line.Trim() == "r") {
        restart_action(use_debounce: false);
      }
    }

    foreach (var watcher in watchers) {
      watcher.EnableRaisingEvents = false;
      watcher.Dispose();
    }

    lock (this) {
      _is_exiting.value = true;
      Subprocess.kill_current();
    }

    thread.Join();
  }

  private void restart_action(bool use_debounce) {
    lock (this) {
      if (use_debounce) {
        if (DateTime.UtcNow - _last_restart < debounce_time) {
          return;
        }
      }
      _last_restart = DateTime.UtcNow;
    }

    _run_action.value = true;
    Subprocess.kill_current();
  }

  private void thread_function() {
    while (true) {
      if (_run_action) {
        _run_action.value = false;
        try {
          action();
        } catch (Exception ex) {
          Log.error(ex.Message);
          Log.info("Action failed but still waiting for changes...");
        }
      }

      if (_is_exiting) {
        break;
      }

      Thread.Sleep(TimeSpan.FromMilliseconds(20));
    }
  }
}
