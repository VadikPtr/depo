using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace depo;

internal static class AstNodeExt {
  internal static string[] unpack_as_string_array(this IList<AstNode> nodes) {
    string[] result = new string[nodes.Count];
    for (int i = 0; i < nodes.Count; i++) {
      if (!nodes[i].is_leaf) {
        throw new InvalidOperationException($"Expected {nodes[i]} to be value expression!");
      }
      result[i] = nodes[i].value;
    }
    return result;
  }

  internal static string[] unpack_as_string_array_skip_flags(this IList<AstNode> nodes) {
    return unpack_as_string_array(nodes).Where(x => !x.StartsWith('\'')).ToArray();
  }

  internal static string unpack_as_string(this IList<AstNode> nodes) {
    if (nodes.Count == 0) {
      throw new InvalidOperationException("Expected to have at least one expression!");
    }
    if (nodes.Count > 1) {
      throw new InvalidOperationException("Expected to have at most one expression!");
    }
    if (!nodes[0].is_leaf) {
      throw new InvalidOperationException($"Expected {nodes[0].value} to be value leaf!");
    }
    return nodes[0].value;
  }

  internal static TEnum parse_flags<TEnum>(this IList<AstNode> nodes)
    where TEnum : struct, Enum {
    Debug.Assert(Enum.GetUnderlyingType(typeof(TEnum)) == typeof(uint));
    Unsafe.SkipInit(out TEnum flags);
    ref uint flags_int = ref Unsafe.As<TEnum, uint>(ref flags);
    foreach (var node in nodes) {
      if (!node.is_leaf) {
        continue;
      }
      if (!node.value.StartsWith('\'')) {
        continue;
      }
      ReadOnlySpan<char> str = node.value;
      str = str[1..];
      if (!Enum.TryParse<TEnum>(str, ignoreCase: true, out var value)) {
        // Console.WriteLine($"Can't parse {str} as {typeof(TEnum).Name}");
        continue;
      }
      flags_int |= Unsafe.As<TEnum, uint>(ref value);
    }
    return flags;
  }

  internal static bool check_os_flags(this IList<AstNode> nodes) {
    OS os = nodes.parse_flags<OS>();
    if (os == OS.None) {
      return true;
    }
    if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) {
      return (os & OS.Mac) != 0;
    }
    if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) {
      return (os & OS.Lin) != 0;
    }
    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
      return (os & OS.Win) != 0;
    }
    throw new Exception($"OS unhandled: {os}");
  }
}
