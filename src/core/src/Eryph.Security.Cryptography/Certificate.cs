using System;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Eryph.Security.Cryptography
{
    public static class Certificate
    {
        public static X509Certificate2 Create(CertificateOptions options)
        {
            var sanBuilder = new SubjectAlternativeNameBuilder();
            sanBuilder.AddIpAddress(IPAddress.Loopback);
            sanBuilder.AddIpAddress(IPAddress.IPv6Loopback);
            sanBuilder.AddDnsName("localhost");
            sanBuilder.AddDnsName(options.Issuer);

            var distinguishedName = new X500DistinguishedName("CN=" + options.Issuer);

            var csp = new CspParameters
            {
                Flags = CspProviderFlags.UseArchivableKey | CspProviderFlags.UseMachineKeyStore
            };
            using (RSA rsa = new RSACryptoServiceProvider(2048, csp))
            {
                var req = new CertificateRequest(distinguishedName, rsa, HashAlgorithmName.SHA256,
                    RSASignaturePadding.Pkcs1);

                req.CertificateExtensions.Add(sanBuilder.Build());

                req.CertificateExtensions.Add(
                    new X509BasicConstraintsExtension(false, false, 0, false));

                req.CertificateExtensions.Add(
                    new X509KeyUsageExtension(
                        X509KeyUsageFlags.KeyAgreement | X509KeyUsageFlags.DataEncipherment |
                        X509KeyUsageFlags.KeyEncipherment, false));

                req.CertificateExtensions.Add(
                    new X509EnhancedKeyUsageExtension(new OidCollection {new Oid("1.3.6.1.5.5.7.3.1")}, true));

                req.CertificateExtensions.Add(
                    new X509SubjectKeyIdentifierExtension(req.PublicKey, false));

                var CA = CreateCA(options);

                var cert = req.Create(
                    CA,
                    CA.NotBefore,
                    CA.NotAfter,
                    CertHelper.SerialNumber.ToByteArray());

                var certWithPK = cert.CopyWithPrivateKey(rsa);
                certWithPK.FriendlyName = options.FriendlyName;

                var certificate = new X509Certificate2(certWithPK.Export(X509ContentType.Pfx, options.Password),
                    options.Password, X509KeyStorageFlags.MachineKeySet);

                CertHelper.AddToMyStore(certificate);

                return certificate;
            }
        }

        private static X509Certificate2 CreateCA(CertificateOptions options)
        {
            var CAFilePath = options.ExportDirectory + "\\" + options.CACertName;
            var create = false;
            if (File.Exists(CAFilePath))
            {
                var CA = new X509Certificate2(CAFilePath, options.Password, X509KeyStorageFlags.Exportable);

                if (CertHelper.IsExpired(CA) == false)
                    return CA;
                create = true;
            }
            else
            {
                create = true;
            }


            if (create)
            {
                var distinguishedName =
                    new X500DistinguishedName("CN=" + string.Concat(options.FriendlyName, options.Suffix));
                using (var rsa = RSA.Create(2048))
                {
                    var parentReq = new CertificateRequest(distinguishedName, rsa, HashAlgorithmName.SHA256,
                        RSASignaturePadding.Pkcs1);

                    parentReq.CertificateExtensions.Add(
                        new X509BasicConstraintsExtension(true, true, 1, true));

                    parentReq.CertificateExtensions.Add(
                        new X509SubjectKeyIdentifierExtension(parentReq.PublicKey, false));

                    parentReq.CertificateExtensions.Add(
                        new X509EnhancedKeyUsageExtension(new OidCollection {new Oid("1.3.6.1.5.5.7.3.1")}, true));

                    parentReq.CertificateExtensions.Add(
                        new X509KeyUsageExtension(
                            X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign |
                            X509KeyUsageFlags.DigitalSignature, false));

                    var caCert = parentReq.CreateSelfSigned(new DateTimeOffset(options.ValidStartDate),
                        new DateTimeOffset(options.ValidEndDate));
                    caCert.FriendlyName = string.Concat(options.FriendlyName, options.Suffix);

                    var caExported = caCert.Export(X509ContentType.Pfx, options.Password);
                    using (Stream stream = File.Create(CAFilePath))
                    {
                        stream.Write(caExported, 0, caExported.Length);
                    }

                    return new X509Certificate2(caCert.Export(X509ContentType.Pfx, options.Password), options.Password,
                        X509KeyStorageFlags.MachineKeySet);
                }
            }

            return null;
        }

        public static void CreateSSL(CertificateOptions options)
        {
            if (CertHelper.ExistsValidCert(options.FriendlyName) == false)
            {
                options.Thumbprint = Create(options).Thumbprint;
                Command.RegisterSSLToUrl(options);
            }
        }
    }
}