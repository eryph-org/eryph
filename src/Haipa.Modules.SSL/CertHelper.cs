using System.Numerics;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Haipa.Modules.SSL
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
        public static bool IsInMyStore(string subjectName)
        {
            X509Store store = ReturnStore();
            X509Certificate2Collection certCollection = store.Certificates.Find(X509FindType.FindBySubjectName,
                                                                                            subjectName, false);
            return certCollection.Count > 0;
        }
        public static X509Certificate2 AddToRootStore(X509Certificate2 certCA)
        {
            var store = new X509Store(StoreName.Root, StoreLocation.LocalMachine);
            store.Open(OpenFlags.OpenExistingOnly | OpenFlags.ReadWrite);
            store.Add(certCA);
            store.Close();
            return certCA;
        }
       
    }
}
