using System.Text.Json.Serialization;

namespace Eryph.Packer;

internal class ArtifactManifest
{
    [JsonPropertyName("format")]
    public string? Format { get; set; }

    [JsonPropertyName("path")]
    public string? Path { get; set; }

    [JsonPropertyName("filename")]
    public string? FileName { get; set; }

    [JsonPropertyName("size")]
    public long? Size { get; set; }

    [JsonPropertyName("parts")]
    public string[]? Parts { get; set; }
}