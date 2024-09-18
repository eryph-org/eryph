using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Crypto;
using X509Certificate = Org.BouncyCastle.X509.X509Certificate;


namespace Eryph.Security.Cryptography;

public interface ICertificateStoreService
{
    bool IsValidRootCertificate(X509Certificate certificate);
    void AddAsRootCertificate(X509Certificate certificate);
    void AddToMyStore(X509Certificate certificate, AsymmetricCipherKeyPair? keyPair=null);
    void RemoveFromMyStore(X509Certificate certificate);
    void RemoveFromRootStore(X509Certificate certificate);

    IEnumerable<X509Certificate> GetFromMyStore(X509Name issuerName);
    IEnumerable<X509Certificate> GetFromRootStore(X509Name distinguishedName);
    IReadOnlyList<X509Certificate2> GetFromMyStore2(X500DistinguishedName subjectName);
    void AddToMyStore(X509Certificate2 certificate);
    void RemoveFromMyStore2(X509Certificate2 certificate);
}