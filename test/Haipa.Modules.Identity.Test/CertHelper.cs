using System.Security.Cryptography.X509Certificates;

namespace Haipa.Modules.Identity.Test
{
    internal static class CertHelper
    {
        public static X509Certificate2 LoadPfx(string name)
        {
            var collection = new X509Certificate2Collection();
            collection.Import($"{name}.pfx", "haipa", X509KeyStorageFlags.PersistKeySet);

            return collection[0];
        }
    }
}