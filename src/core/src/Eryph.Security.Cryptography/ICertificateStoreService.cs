using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;

namespace Eryph.Security.Cryptography;

public interface ICertificateStoreService
{
    void AddToMyStore(X509Certificate2 certificate);

    void AddToRootStore(X509Certificate2 certificate);

    IReadOnlyList<X509Certificate2> GetFromMyStore(X500DistinguishedName subjectName);

    IReadOnlyList<X509Certificate2> GetFromRootStore(X500DistinguishedName subjectName);

    void RemoveFromMyStore(X500DistinguishedName subjectName);

    void RemoveFromMyStore(PublicKey subjectKey);

    void RemoveFromRootStore(X500DistinguishedName subjectName);

    void RemoveFromRootStore(PublicKey subjectKey);
}
