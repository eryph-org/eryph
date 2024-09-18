using System;
using System.IO;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Eryph.Modules.Identity.Services;

namespace Eryph.Modules.Identity.Test.Integration;

internal class TestCertificateManager : ISigningCertificateManager
{


    public Task<X509Certificate2> GetSigningCertificate(string _)
    {
        var collection = new X509Certificate2Collection();
        collection.Import(Path.Combine(Path.GetDirectoryName(GetType().Assembly.Location)!, "testsigning.pfx")
            , "test", X509KeyStorageFlags.PersistKeySet);

        return Task.FromResult(collection[0]);
    }
}