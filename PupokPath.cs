using System.Diagnostics;

namespace DepoBCS;

public static class PupokPath {
  private const string sep_str = "/";
  private const char   sep     = '/';

  public static string join(string first, string second) {
    Debug.Assert(first.Length > 0 && second.Length > 0, "should have dealt with empty paths");
    var has_separator = is_directory_separator(first[^1]) || is_directory_separator(second[0]);
    return has_separator
      ? string.Concat(first, second)
      : string.Concat(first, sep_str, second);
  }

  public static string join(string first, string second, string third) {
    Debug.Assert(first.Length > 0 && second.Length > 0 && third.Length > 0, "should have dealt with empty paths");
    var first_has_separator  = is_directory_separator(first[^1]) || is_directory_separator(second[0]);
    var second_has_separator = is_directory_separator(second[^1]) || is_directory_separator(third[0]);
    return (first_has_separator, second_has_separator) switch {
      (false, false) => string.Concat(first, sep_str, second, sep_str, third),
      (false, true)  => string.Concat(first, sep_str, second, third),
      (true, false)  => string.Concat(first, second, sep_str, third),
      (true, true)   => string.Concat(first, second, third),
    };
  }

  public static unsafe string normalize(string path) {
    var result = stackalloc char[path.Length + 1];
    var source = path.AsSpan();

    for (var i = 0; i < source.Length; i++) {
      result[i] = source[i] != '\\'
        ? source[i]
        : sep;
    }

    result[path.Length] = '\0';
    var result_str = new string(result);
    Debug.Assert(result_str.Length == path.Length);
    return result_str;
  }

  public static string parent(string path) {
    var last_split = -1;

    for (var index = path.Length - 1; index >= 0; --index) {
      if (path[index] is not ('\\' or '/'))
        continue;
      last_split = index;
      break;
    }

    return last_split != -1
      ? path[..last_split]
      : string.Empty;
  }

  private static bool is_directory_separator(char value) => sep == value;
}

public class PupokPathWrapper(string path) {
  public string value { get; set; } = path;

  public override                 string ToString()                => value;
  public static implicit operator string(PupokPathWrapper wrapper) => wrapper.value;
  public static implicit operator PupokPathWrapper(string value)   => new(value);

  public PupokPathWrapper parent()                                     => new(PupokPath.parent(value));
  public PupokPathWrapper normalized()                                 => new(PupokPath.normalize(value));
  public PupokPathWrapper join(PupokPathWrapper a)                     => new(PupokPath.join(value, a.value));
  public PupokPathWrapper join(PupokPathWrapper a, PupokPathWrapper b) => new(PupokPath.join(value, a.value, b.value));
  public string           get_file_name_without_ext() => Path.GetFileNameWithoutExtension(value);
  public string           get_file_name()             => Path.GetFileName(value);
}
