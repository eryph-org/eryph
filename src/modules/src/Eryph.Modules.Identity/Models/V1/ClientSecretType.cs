using JetBrains.Annotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Eryph.Modules.Identity.Models.V1
{
    [JsonConverter(typeof(StringEnumConverter))]
    [PublicAPI]
    public enum ClientSecretType
    {
        RsaPrivateKey,
        SharedKey
    }
}