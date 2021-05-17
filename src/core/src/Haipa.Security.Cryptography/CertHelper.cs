using System;
using System.IO;
using System.Numerics;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.OpenSsl;
using Org.BouncyCastle.X509;

namespace Haipa.Security.Cryptography
{
    public static class CertHelper
    {
        public static BigInteger SerialNumber 
        {
            get
            {
                var rng = new RNGCryptoServiceProvider();
                byte[] bytes = new byte[16];
                rng.GetBytes(bytes);
                return new BigInteger(bytes);
            }
        }        
        private static X509Store ReturnStore()
        {
            var store = new X509Store(StoreName.My, StoreLocation.LocalMachine);
            store.Open(OpenFlags.OpenExistingOnly | OpenFlags.ReadWrite);
            return store;
        }
        public static X509Certificate2 AddToMyStore(X509Certificate2 cert)
        {
            X509Store store = ReturnStore();
            store.Add(cert);
            store.Close();
            return cert;
        }
        public static void RemoveFromMyStore(X509Certificate2 certificate2)
        {
            X509Store store = ReturnStore();
            store.Remove(certificate2);
            store.Close();
        }
        public static bool IsInMyStore(string IssuerName)
        {
            X509Store store = ReturnStore();
            X509Certificate2Collection certCollection = store.Certificates.Find(X509FindType.FindByIssuerName,
                                                                                            IssuerName, false);
            return certCollection.Count > 0;
        }
        public static bool ExistsValidCert(string IssuerName)
        {
            X509Store store = ReturnStore();
            X509Certificate2Collection certCollection = store.Certificates.Find(X509FindType.FindByIssuerName,
                                                                                            IssuerName, false);
            if ( certCollection.Count > 0)
            {
                foreach (var item in certCollection)
                {
                   if (IsExpired(item) == false)
                    {
                        return true;
                    }
                }
                return false;
            }
            else { return false; }
        }
        public static bool IsExpired(X509Certificate2 cert)
        {
            DateTime expireDate = System.Convert.ToDateTime(cert.GetExpirationDateString());
            if (DateTime.Now > expireDate)
            {
                return true;
            }
            else { return false; }
        }

        public static AsymmetricCipherKeyPair ReadPrivateKeyFile(string filepath)
        {    

            using (var reader = File.OpenText(filepath))
                return (AsymmetricCipherKeyPair)new PemReader(reader).ReadObject();

        }

        public static void WritePrivateKeyFile(string filepath, AsymmetricCipherKeyPair keyPair)
        {
            using (var writer = new StreamWriter(filepath))
                new PemWriter(writer).WriteObject(keyPair);

        }

        public static AsymmetricKeyParameter GetPublicKey(string certData)
        {
            var parser = new X509CertificateParser();
            var cert2 = parser.ReadCertificate(Convert.FromBase64String(certData));
            return cert2.GetPublicKey();
        }

    }
}
