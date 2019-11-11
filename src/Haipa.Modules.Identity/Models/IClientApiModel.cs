

namespace Haipa.Modules.Identity.Models
{
    public interface IClientApiModel
    {
        string ClientId { get; set; }
        string Description { get; set; }
        string X509CertificateBase64 { get; set; }
        string[] AllowedScopes { get; set; }
    }
}
