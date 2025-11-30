using System.Runtime.InteropServices;

namespace DepoBCS;

internal static class KindExt {
  public static string wrap(this string name, Kind kind) {
    if (name.EndsWith(".dll") || name.EndsWith(".exe") || name.EndsWith(".lib")) {
      return name; // already formatted
    }
    switch (kind) {
      case Kind.Dll:
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
          return name + ".dll";
        }
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) {
          return "lib" + name + ".so";
        }
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) {
          return "lib" + name + ".dylib";
        }
        break;
      case Kind.Lib:
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
          return name + ".lib";
        }
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ||
            RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) {
          return "lib" + name + ".a";
        }
        break;
      case Kind.Exe:
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
          return name + ".exe";
        }
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ||
            RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) {
          return name;
        }
        break;
    }
    throw new ArgumentOutOfRangeException(nameof(kind));
  }
}
