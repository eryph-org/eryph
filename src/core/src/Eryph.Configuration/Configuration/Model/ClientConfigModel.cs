using System;

namespace Eryph.Configuration.Model
{
    public class ClientConfigModel
    {
        public string ClientId { get; set; }
        public string ClientName { get; set; }
        public string Description { get; set; }
        public string X509CertificateBase64 { get; set; }
        public string SharedSecret { get; set; }
        public string[] AllowedScopes { get; set; }
        public Guid[] Roles { get; set; }
        public Guid TenantId { get; set; }
    }
}