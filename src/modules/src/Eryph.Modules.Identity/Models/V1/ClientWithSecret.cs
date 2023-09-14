using JetBrains.Annotations;

namespace Eryph.Modules.Identity.Models.V1;

[PublicAPI]
public class ClientWithSecret : Client
{
    /// <summary>
    ///     private Key of client
    /// </summary>
    public string Key { get; set; }

}