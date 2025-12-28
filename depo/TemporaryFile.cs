namespace depo;

public sealed class TemporaryFile(string ext = "") : IDisposable {
  public string path { get; private set; } =
    Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ext);

  ~TemporaryFile() {
    delete();
  }

  public void Dispose() {
    delete();
    GC.SuppressFinalize(this);
  }

  private void delete() {
    if (path == null) {
      return;
    }
    File.Delete(path);
    path = null;
  }
}
