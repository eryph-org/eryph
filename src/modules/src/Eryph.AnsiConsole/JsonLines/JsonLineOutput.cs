using System.Text.Json.Serialization;

namespace Eryph.AnsiConsole.JsonLines;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(JsonLineInfo), "info")]
[JsonDerivedType(typeof(JsonLineResult), "result")]
[JsonDerivedType(typeof(JsonLineError), "error")]
internal abstract class JsonLineOutput;
