using System.Text.Json.Serialization;
using JetBrains.Annotations;


namespace Eryph.Modules.Identity.Models.V1
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    [PublicAPI]
    public enum ClientSecretType
    {
        RsaPrivateKey,
        SharedKey
    }
}