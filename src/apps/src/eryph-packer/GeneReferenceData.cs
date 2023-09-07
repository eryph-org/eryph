using System.Text.Json.Serialization;

namespace Eryph.Packer;

public class GeneReferenceData
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("hash")]
    public string? Hash { get; set; }


}