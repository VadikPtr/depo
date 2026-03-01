namespace depo;

public enum BuildConfig {
  Debug,
  Release,
}

internal class SolutionContext {
  public readonly DepoM       model;
  public readonly BuildConfig config;
  public readonly string      build_directory;
  public readonly string      bin_directory;
  public readonly string      obj_directory;
  public readonly string      target;

  public SolutionContext(DepoM model, BuildConfig config, string target) {
    this.model      = model;
    this.config     = config;
    this.target     = target;
    build_directory = Path.Join(model.dir, "build", config.ToString());
    bin_directory   = Path.Join(model.dir, "bin", config.ToString());
    obj_directory   = Path.Join(build_directory, "obj");
    Directory.CreateDirectory(build_directory);
    Directory.CreateDirectory(obj_directory);
    if (this.target == null && model.targets.Length != 0) {
      this.target = model.targets[0];
    }
    Log.info($"Target: {target}");
    Log.info($"Build directory: {build_directory}");
  }

  public void generate() {
    List<NinjaGenerator> projects = [];
    try {
      foreach (var model_project in model.projects) {
        if (model_project.kind != Kind.Iface) {
          projects.Add(new NinjaGenerator(model_project, this));
        }
      }
      foreach (var project in projects) {
        project.write();
      }
    } finally {
      foreach (var project in projects) {
        project.Dispose();
      }
    }

    write_solution_file(projects);
  }

  public void dump_compile_commands() {
    Log.info("Writing compile commands...");
    var output = Subprocess.run(DepoTool.ninja, "-C", Path.Join(build_directory), "-t", "compdb").check();
    File.WriteAllText("compile_commands.json", output.stdout);
    Log.info("Writing compile commands finished.");
  }

  public void build() {
    Log.info("Running build...");
    Subprocess.run_console_out(DepoTool.ninja, "-C", Path.Join(build_directory), "-v", target); // "-d", "explain" 
    Log.info("Build finished.");
  }

  private void write_solution_file(List<NinjaGenerator> projects) {
    using var file   = File.Open(Path.Join(build_directory, "build.ninja"), FileMode.Create, FileAccess.Write);
    using var writer = new StreamWriter(file);
    foreach (var project in projects) {
      writer.Write($"subninja ./{Path.GetFileName(project.project_file)}\n");
    }
    if (model.targets.Length != 0) {
      writer.Write($"default {model.targets[0]}\n");
    }
  }
}
