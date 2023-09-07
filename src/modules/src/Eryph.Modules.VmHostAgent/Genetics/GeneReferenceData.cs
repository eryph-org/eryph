using System.Text.Json.Serialization;

namespace Eryph.Modules.VmHostAgent.Genetics;

public class GeneReferenceData
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("hash")]
    public string? Hash { get; set; }


}