using System.Collections.Generic;
using System.Threading.Tasks;
using IdentityServer4;
using IdentityServer4.Models;
using IdentityServer4.Stores;

namespace Haipa.Modules.Identity
{
    public class ClientStoreWrapper : IClientStore
    {
        public ClientStoreWrapper()
        {

        }

        public Task<Client> FindClientByIdAsync(string clientId)
        {
            if (clientId == "console")
                return Task.FromResult(new Client()
                {
                    ClientId = "console",
                    //ClientSecrets = new List<Secret>(new Secret[] { new Secret("peng".Sha256()), }),
                    ClientSecrets = new List<Secret>(new []{ new Secret
                    {
                        Type = IdentityServerConstants.SecretTypes.X509CertificateBase64, 
                        Value = "MIIDODCCAiCgAwIBAgIJAOpQ0eFJaKxwMA0GCSqGSIb3DQEBCwUAMBQxEjAQBgNVBAMTCWxvY2FsaG9zdDAeFw0xOTAxMTQxMzQzMTdaFw0yMDAxMTQxMzQzMTdaMBQxEjAQBgNVBAMTCWxvY2FsaG9zdDCCASIwDQYJKoZIhvcNAQEBBQADggEPADCCAQoCggEBAMPqtSGct4mEbI82borT+PtlGFWPgDvzmaufzuXq5bHmsrLO4M6JmNjSIP5GCnKyZFGdOGPUZJ1ixxMPYhxiMRDCAoXWiU5TNCGIGlU6FimXlotvocqID8wGM4i//nxtK+hpxLyOwB87bGVG2Xuz3Fy/zT9GSGprDM10gBLdWPBYudK3lV5DTvYetKCO/XKziB/m3H4CKUWDJB3MawBRgZHxTHCI7qfxuBjxKSpe6W3kx5vO7t0MEIryJWAl/35HZoIC3FmjqQOBrZCzs1oI3pCLpY9aRspmwrHTnePD56f/s1rbQEsVSzKunoBl8mvyPJkHADAceLZuR4jIv3VzUtUCAwEAAaOBjDCBiTAMBgNVHRMBAf8EAjAAMA4GA1UdDwEB/wQEAwIFIDAWBgNVHSUBAf8EDDAKBggrBgEFBQcDATAXBgNVHREBAf8EDTALgglsb2NhbGhvc3QwOAYKKwYBBAGCN1QBAQQqQVNQLk5FVCBDb3JlIEhUVFBTIGRldmVsb3BtZW50IGNlcnRpZmljYXRlMA0GCSqGSIb3DQEBCwUAA4IBAQCwTq5AvWo03e5cFz+vIWbYGq33LQfdv1OE03o+pqXRa/0vbg4zNNOmKOV0OdicWG3eCVGztau5Z0EWYwoVGlB6iBuRRY2eKgk924qgFkrpe/wZ6SIEklEOBVj5vL30i8HO/G8IWgl9+OHdhx14YyEkpQoLtkcsEPSsWPMRlp0TF9Roawi1wrfLphbHxRDV10BIHwy3A6aLrxRLg6RSP6GjRTlMr4qwf0KPG/CtSyGI0+caqWGn+M1S/RuArUsF2QaeMpIUglHOrEFuNJlb+kH1xHWZDkcwY3KLcxL5L9LZfB1OZQ3l18nad0S0FZU6SMfiMk5daRRNDwYTEaWahrv2"
                    }}),
                    AllowedGrantTypes = GrantTypes.ClientCredentials,
                    AllowOfflineAccess = true,                    
                    AllowedScopes = {"openid","identity:apps:read:all","compute_api"},
                    AllowRememberConsent = true,
                    RequireConsent = false,
                   
                });

            return null;
        }
    }
}