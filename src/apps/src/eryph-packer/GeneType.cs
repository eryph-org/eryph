using System.Text.Json.Serialization;

namespace Eryph.Packer;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum GeneType
{
    Catlet,
    Volume,
    Fodder
}