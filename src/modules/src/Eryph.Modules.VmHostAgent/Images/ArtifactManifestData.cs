using System.Text.Json.Serialization;

namespace Eryph.Modules.VmHostAgent.Images;

public class ArtifactManifestData
{
    [JsonPropertyName("format")]
    public string Format { get; set; }

    [JsonPropertyName("path")]
    public string Path { get; set; }

    [JsonPropertyName("filename")]
    public string FileName { get; set; }


    [JsonPropertyName("parts")]
    public string[] Parts { get; set; }



    [JsonPropertyName("size")]
    public long Size { get; set; }

}