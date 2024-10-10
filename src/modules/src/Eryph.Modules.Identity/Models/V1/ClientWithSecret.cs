using JetBrains.Annotations;

namespace Eryph.Modules.Identity.Models.V1;

[PublicAPI]
public class ClientWithSecret : Client
{
    /// <summary>
    /// The private key or shared secret of the client.
    /// </summary>
    public required string Key { get; set; }
}
