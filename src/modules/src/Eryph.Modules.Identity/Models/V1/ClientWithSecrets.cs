using JetBrains.Annotations;

namespace Eryph.Modules.Identity.Models.V1;

[PublicAPI]
public class ClientWithSecrets : Client
{
    /// <summary>
    ///     private Key of client
    /// </summary>
    public string Key { get; set; }


    /// <summary>
    ///     type of the client key
    /// </summary>
    public ClientSecretType KeyType { get; set; }
}