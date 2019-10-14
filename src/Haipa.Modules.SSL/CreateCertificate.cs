using System;
using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Haipa.Modules.SSL
{
    public static class CreateCertificate
    {
        public static X509Certificate2 Create(CertificateOptions options)
        {
            SubjectAlternativeNameBuilder sanBuilder = new SubjectAlternativeNameBuilder();
            sanBuilder.AddIpAddress(IPAddress.Loopback);
            sanBuilder.AddIpAddress(IPAddress.IPv6Loopback);
            sanBuilder.AddDnsName("localhost");
            sanBuilder.AddDnsName(options.Issuer);

            X500DistinguishedName distinguishedName = new X500DistinguishedName($"CN=" + options.Issuer);

            var csp = new CspParameters
            {
                Flags = CspProviderFlags.UseArchivableKey | CspProviderFlags.UseMachineKeyStore
            };
            using (RSA rsa = new RSACryptoServiceProvider(2048, csp))
            {
                CertificateRequest req = new CertificateRequest(distinguishedName, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

                req.CertificateExtensions.Add(sanBuilder.Build());

                req.CertificateExtensions.Add(
                    new X509BasicConstraintsExtension(false, false, 0, false));

                req.CertificateExtensions.Add(
                    new X509KeyUsageExtension(X509KeyUsageFlags.KeyAgreement | X509KeyUsageFlags.DataEncipherment | X509KeyUsageFlags.KeyEncipherment, false));

                req.CertificateExtensions.Add(
                    new X509EnhancedKeyUsageExtension(new OidCollection { new Oid("1.3.6.1.5.5.7.3.1") }, true));

                req.CertificateExtensions.Add(
                    new X509SubjectKeyIdentifierExtension(req.PublicKey, false));

                X509Certificate2 cert = req.Create(
                    CreateCA(options),
                    new DateTimeOffset(options.ValidStartDate), 
                    new DateTimeOffset(options.ValidEndDate), 
                   CertHelper.SerialNumber.ToByteArray());

                X509Certificate2 certWithPK = cert.CopyWithPrivateKey(rsa);
                certWithPK.FriendlyName = options.FriendlyName;

                X509Certificate2 certificate = new X509Certificate2(certWithPK.Export(X509ContentType.Pfx, options.Password), options.Password, X509KeyStorageFlags.MachineKeySet);
                
                CertHelper.AddToMyStore(certificate);

                return certificate;
            }
        }
        private static X509Certificate2 CreateCA(CertificateOptions options)
        {
            string suffix = " CA";
            X500DistinguishedName distinguishedName = new X500DistinguishedName($"CN=" + string.Concat(options.Issuer, suffix));
            using (RSA rsa = RSA.Create(2048))
            {
                CertificateRequest parentReq = new CertificateRequest(distinguishedName, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

                parentReq.CertificateExtensions.Add(
                    new X509BasicConstraintsExtension(true, true, 1, true));

                parentReq.CertificateExtensions.Add(
                    new X509SubjectKeyIdentifierExtension(parentReq.PublicKey, false));

                parentReq.CertificateExtensions.Add(
                    new X509EnhancedKeyUsageExtension(new OidCollection { new Oid("1.3.6.1.5.5.7.3.1") }, true));

                parentReq.CertificateExtensions.Add(
                   new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign | X509KeyUsageFlags.DigitalSignature, false));

                X509Certificate2 caCert = parentReq.CreateSelfSigned(new DateTimeOffset(options.ValidStartDate), new DateTimeOffset(options.ValidEndDate));
                caCert.FriendlyName = string.Concat(options.FriendlyName, suffix);

                CertHelper.AddToRootStore(caCert);

                return new X509Certificate2(caCert.Export(X509ContentType.Pfx, options.Password), options.Password, X509KeyStorageFlags.MachineKeySet);
            }
        }
    }
}
