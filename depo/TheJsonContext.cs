using System.Text.Json.Serialization;

namespace depo;

[JsonSourceGenerationOptions(
  WriteIndented = true,
  GenerationMode = JsonSourceGenerationMode.Serialization | JsonSourceGenerationMode.Metadata,
  DefaultIgnoreCondition = JsonIgnoreCondition.Never,
  IncludeFields = true
)]
[JsonSerializable(typeof(DepoM))]
[JsonSerializable(typeof(MsvcProduct[]))]
[JsonSerializable(typeof(ProjectM))]
internal partial class TheJsonContext : JsonSerializerContext;
  