using System.Text.Json.Serialization;

namespace Eryph.Modules.VmHostAgent.Genetics;

public class GeneSetManifestData
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
}