using System.Text.Json.Serialization;

namespace Eryph.AnsiConsole.JsonLines;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(JsonLineInfo), typeDiscriminator: "info")]
[JsonDerivedType(typeof(JsonLineResult), typeDiscriminator: "result")]
[JsonDerivedType(typeof(JsonLineError), typeDiscriminator: "error")]
public abstract class JsonLineOutput
{
}
