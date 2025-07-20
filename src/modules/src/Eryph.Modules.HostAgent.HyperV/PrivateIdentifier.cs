using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace Eryph.Modules.VmHostAgent;

public class PrivateIdentifier
{
    [JsonProperty("_pi")]
    [JsonPropertyName("_pi")]
    public PrivateIdentifierValue Value { get; set; }


}