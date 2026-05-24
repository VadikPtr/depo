using System.Text.RegularExpressions;

namespace depo;

internal static class EnumGenerator {
  private record EnumValue(int priority, string name);

  internal static void generate(GenerateEnum generate_enum, HashSet<string> files) {
    EnumValue[] enum_keys = extract_enum_keys(generate_enum.macro_name, files);
    string      enum_name = get_enum_name(generate_enum.macro_name);
    string      output    = generate_file_content(enum_name, enum_keys);
    write_content(output, generate_enum.out_path);
  }

  private static EnumValue[] extract_enum_keys(string macro_name, HashSet<string> files) {
    List<EnumValue> enums_keys = [];
    foreach (var file in files) {
      string          text    = File.ReadAllText(file);
      MatchCollection matches = Regex.Matches(text, $@"{macro_name}\(([^)]*)\)");
      foreach (Match match in matches) {
        string value = match.Groups[1].Value;
        if (value.Contains('.')) {
          continue;
        }
        int priority = 0;
        if (value.Contains(',')) {
          var values = value.Split(',');
          if (values.Length > 1 && int.TryParse(values[1], out priority)) {
            value = values[0].Trim();
          }
        }
        enums_keys.Add(new EnumValue(priority, value));
      }
    }
    return enums_keys.DistinctBy(x => x.name)
      .OrderByDescending(x => x.priority)
      .ThenBy(x => x.name)
      .ToArray();
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

  private static string generate_file_content(string enum_name, EnumValue[] enum_keys) {
    var enum_values = string.Join('\n', enum_keys.Select((x, i) => {
      var priority_string = "";
      if (x.priority != 0) {
        priority_string = $" /* priority:{x.priority} */";
      }
      return $"  {x.name} = {i},{priority_string}";
    }));
    var x_enum_values = string.Join(" \\\n", enum_keys.Select(x => $"  X({x.name})"));
    return
      $$"""
      #pragma once

      enum class {{enum_name}} {
      {{enum_values}}
      };

      inline constexpr size_t {{enum_name}}Count = {{enum_keys.Length}};

      #define x{{enum_name}}(X) \
      {{x_enum_values}}

      """;
  }

  private static string get_enum_name(string macro_name) {
    return macro_name[1..];
  }
}
