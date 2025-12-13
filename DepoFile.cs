using System.Text.Json;

namespace depo;

internal class DepoFile {
  private string _root_dir;

  internal DepoM parse() {
    _root_dir = Environment.CurrentDirectory;

    var the_depo     = new DepoM { dir = _root_dir };
    var dir_hash     = new HashSet<string>();
    var require_dirs = new Queue<string>();
    require_dirs.Enqueue(_root_dir);

    while (require_dirs.Count != 0) {
      var dir = require_dirs.Dequeue();
      if (!dir_hash.Add(dir)) {
        continue;
      }

      var expr = read_model(dir);
      if (expr == null) {
        Log.info("Cannot read {0}", dir);
        continue;
      }
      var model = expr.call(dir);

      the_depo.projects.AddRange(model.projects);
      the_depo.bin.AddRange(model.bin);
      if (dir == _root_dir) {
        the_depo.targets      = model.targets;
        the_depo.archive_deps = model.archive_deps;
        the_depo.git_deps     = model.git_deps;
        the_depo.svn_deps     = model.svn_deps;
      }

      foreach (var require in model.require) {
        require_dirs.Enqueue(require);
      }
    }

    the_depo.projects.Reverse();
    if (Log.is_debug) {
      Log.debug("TheDepo: {0}", JsonSerializer.Serialize(the_depo, TheJsonContext.Default.DepoM));
    }
    return the_depo;
  }

  private static DepoAction read_model(string dir) {
    string path = Path.Join(dir, "depo.lisp");
    try {
      Log.debug("Parsing {0}", path);
      string text = File.ReadAllText(path);
      return Parser.parse(text);
    } catch (Exception ex) {
      Log.error($"Error: failed to parse {path}. {ex.Message}");
      return null;
    }
  }
}
