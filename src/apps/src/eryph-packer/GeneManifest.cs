using System.Text.Json.Serialization;

namespace Eryph.Packer;

internal class GeneManifest
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }


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

    [JsonPropertyName("metadata")]
    public Dictionary<string, string>? Metadata { get; set; }

}