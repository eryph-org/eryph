using System.Collections.Generic;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.X509;


namespace Eryph.Security.Cryptography;

public interface ICertificateStoreService
{
    bool IsValidRootCertificate(X509Certificate certificate);
    void AddAsRootCertificate(X509Certificate certificate);
    void AddToMyStore(X509Certificate certificate, AsymmetricCipherKeyPair keyPair);
    void RemoveFromMyStore(X509Certificate certificate);
    void RemoveFromRootStore(X509Certificate certificate);

    IEnumerable<X509Certificate> GetFromMyStore(X509Name issuerName);
    IEnumerable<X509Certificate> GetFromRootStore(X509Name distinguishedName);
}