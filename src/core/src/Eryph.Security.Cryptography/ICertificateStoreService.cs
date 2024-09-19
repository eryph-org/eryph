using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;

namespace Eryph.Security.Cryptography;

public interface ICertificateStoreService
{
    IReadOnlyList<X509Certificate2> GetFromMyStore(X500DistinguishedName subjectName);
    void AddToMyStore(X509Certificate2 certificate);
    void RemoveFromMyStore(X509Certificate2 certificate);
    void AddToRootStore(X509Certificate2 certificate);
}
