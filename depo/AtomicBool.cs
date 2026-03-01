namespace depo;

public class AtomicBool {
  private long _value = 0;

  public AtomicBool() { }

  public AtomicBool(bool default_value) {
    _value = default_value ? 1 : 0;
  }

  public bool value {
    get => Interlocked.Read(ref _value) == 1;
    set => Interlocked.Exchange(ref _value, value ? 1 : 0);
  }

  public bool get_and_set(bool new_value) {
    long previous = Interlocked.Exchange(ref _value, new_value ? 1 : 0);
    return previous != 0;
  }

  public static implicit operator bool(AtomicBool x) {
    return x.value;
  }
}
