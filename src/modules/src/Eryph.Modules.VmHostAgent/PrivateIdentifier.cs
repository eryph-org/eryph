using Newtonsoft.Json;

namespace Eryph.Modules.VmHostAgent;

public class PrivateIdentifier
{
    [JsonProperty("_pi")]
    public PrivateIdentifierValue Value { get; set; }


}