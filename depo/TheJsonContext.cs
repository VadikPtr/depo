using System.Text.Json.Serialization;

namespace depo;

[JsonSourceGenerationOptions(
  WriteIndented = true,
  GenerationMode = JsonSourceGenerationMode.Serialization,
  DefaultIgnoreCondition = JsonIgnoreCondition.Never,
  IncludeFields = true
)]
[JsonSerializable(typeof(DepoM))]
[JsonSerializable(typeof(ProjectM))]
internal partial class TheJsonContext : JsonSerializerContext;
