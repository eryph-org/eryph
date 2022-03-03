using System.Text.Json.Serialization;

namespace Eryph.Modules.VmHostAgent.Images;

public class ImageManifestData
{
    [JsonPropertyName("image")]
    public string Image { get; set; }

    [JsonPropertyName("ref")]
    public string Reference { get; set; }

    [JsonPropertyName("artifacts")]
    public string[] Artifacts { get; set; }

}