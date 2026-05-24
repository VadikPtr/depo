using System.Text.RegularExpressions;

namespace depo;

internal static partial class EnumGenerator {
  internal static void generate(GenerateEnum generate_enum, HashSet<string> files) {
    SortedSet<string> enum_keys = extract_enum_keys(generate_enum.macro_name, files);
    string            enum_name = get_enum_name(generate_enum.macro_name);
    string            output    = generate_file_content(enum_name, enum_keys);
    write_content(output, generate_enum.out_path);
  }

  private static SortedSet<string> extract_enum_keys(string macro_name, HashSet<string> files) {
    SortedSet<string> enums_keys = [];
    foreach (var file in files) {
      string          text    = File.ReadAllText(file);
      MatchCollection matches = Regex.Matches(text, $@"{macro_name}\(([^)]*)\)");
      foreach (Match match in matches) {
        string value = match.Groups[1].Value;
        if (value.Contains('.')) {
          continue;
        }
        enums_keys.Add(value);
      }
    }
    return enums_keys;
  }

  private static void write_content(string output, string out_path) {
    var existing_file_hash = Hash.get_file_hash(out_path);
    var output_hash        = Hash.get_string_hash(output);
    if (existing_file_hash.SequenceEqual(output_hash)) {
      Log.info($"Generated enum {out_path} already contains actual enums");
      return;
    }
    File.WriteAllText(out_path, output);
  }

  private static string generate_file_content(string enum_name, SortedSet<string> enum_keys) {
    var enum_values   = string.Join('\n', enum_keys.Select((x,    i) => $"  {x} = {i},"));
    var x_enum_values = string.Join(" \\\n", enum_keys.Select((x, i) => $"  X({x})"));
    return
      $$"""
      #pragma once

      enum class {{enum_name}} {
      {{enum_values}}
      };

      inline constexpr size_t {{enum_name}}Count = {{enum_keys.Count}};

      #define x{{enum_name}}(X) \
      {{x_enum_values}}

      """;
  }

  private static string get_enum_name(string macro_name) {
    return macro_name[1..];
  }
}
