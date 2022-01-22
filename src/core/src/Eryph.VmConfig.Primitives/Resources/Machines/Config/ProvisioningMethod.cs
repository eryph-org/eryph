using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Eryph.Resources.Machines.Config
{
    [JsonConverter(typeof(StringEnumConverter))]
    public enum ProvisioningMethod
    {
        None = 0,
        CloudInit = 1
    }
}