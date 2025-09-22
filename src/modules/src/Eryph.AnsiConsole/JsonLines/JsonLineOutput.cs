using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Eryph.AnsiConsole.JsonLines;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(JsonLineInfo), typeDiscriminator: "info")]
[JsonDerivedType(typeof(JsonLineResult), typeDiscriminator: "result")]
public abstract class JsonLineOutput
{
}
