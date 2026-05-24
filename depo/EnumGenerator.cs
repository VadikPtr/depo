using System.Text.RegularExpressions;

namespace depo;

internal static class EnumGenerator {
  private record EnumValue(int priority, string name);

  internal static void generate(GenerateEnum generate_enum, ProjectM project) {
    HashSet<string> file_list = get_file_list(generate_enum, project);
    string[] file_contents = file_list.Select(File.ReadAllText).ToArray();
    List<string> outputs = new List<string>(capacity: generate_enum.macro.Length);

    foreach (var macro_name in generate_enum.macro) {
      EnumValue[] enum_keys = extract_enum_keys(macro_name, file_contents);
      string enum_name = macro_name[1..];
      string output = generate_file_content(enum_name, enum_keys);
      outputs.Add(output);
    }

    write_content(outputs, generate_enum.out_path);
  }

  private static HashSet<string> get_file_list(GenerateEnum generate_enum, ProjectM project) {
    HashSet<string> file_list = [];
    foreach (var requested_file in generate_enum.files) {
      if (requested_file == "'project-files") {
        foreach (var path in project.files) {
          file_list.Add(path);
        }
      } else if (requested_file.Contains('*')) {
        foreach (var path in Directory.EnumerateFiles(project.depo.dir, requested_file, SearchOption.AllDirectories)) {
          file_list.Add(path);
        }
      } else {
        file_list.Add(Path.Join(project.depo.dir, requested_file));
      }
    }
    return file_list;
  }

  private static EnumValue[] extract_enum_keys(string macro_name, string[] file_contents) {
    List<EnumValue> enums_keys = [];
    foreach (var text in file_contents) {
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

  private static void write_content(List<string> outputs, string out_path) {
    string output = string.Join('\n', outputs.Prepend("#include <cstddef>").Prepend("#pragma once"));
    byte[] existing_file_hash = Hash.get_file_hash(out_path);
    byte[] output_hash = Hash.get_string_hash(output);
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
      enum class {{enum_name}} {
      {{enum_values}}
      };

      inline constexpr size_t {{enum_name}}Count = {{enum_keys.Length}};

      #define x{{enum_name}}(X) \
      {{x_enum_values}}

      """;
  }
}
