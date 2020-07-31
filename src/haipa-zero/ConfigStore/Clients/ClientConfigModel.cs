namespace Haipa.Runtime.Zero.ConfigStore.Clients
{
    public class ClientConfigModel
    {
        public string ClientId { get; set; }
        public string ClientName { get; set; }
        public string Description { get; set; }
        public string X509CertificateBase64 { get; set; }
        public string[] AllowedScopes { get; set; }
    }
}