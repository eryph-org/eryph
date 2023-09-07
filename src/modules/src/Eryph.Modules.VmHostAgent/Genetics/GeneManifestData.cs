using System.Text.Json.Serialization;
using Eryph.Resources;

namespace Eryph.Modules.VmHostAgent.Genetics;

public class GeneManifestData
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("type")]
    public GeneType? Type { get; set; }


    [JsonPropertyName("format")]
    public string? Format { get; set; }


    [JsonPropertyName("filename")]
    public string? FileName { get; set; }


    [JsonPropertyName("parts")]
    public string[]? Parts { get; set; }


    [JsonPropertyName("size")]
    public long? Size { get; set; }

    [JsonPropertyName("original_size")]
    public long? OriginalSize { get; set; }
}