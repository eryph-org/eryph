using System.Text.Json.Serialization;

namespace Eryph.Packer;

public class GeneSetManifest
{
    [JsonPropertyName("geneset")]
    public string? GeneSet { get; set; }

    [JsonPropertyName("ref")]
    public string? Reference { get; set; }

    [JsonPropertyName("volumes")]
    public GeneReferenceData[]? VolumeGenes { get; set; }

    [JsonPropertyName("fodder")]
    public GeneReferenceData[]? FodderGenes { get; set; }

    [JsonPropertyName("catlet")]
    public string? CatletGene { get; set; }

    [JsonPropertyName("parent")]

    public string? Parent { get; set; }

    [JsonPropertyName("metadata")]
    public Dictionary<string,string>? Metadata { get; set; }


}

public class GeneReferenceData
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("hash")]
    public string? Hash { get; set; }


}