using System;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Operators;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.X509;

namespace Eryph.Security.Cryptography;

public class CertificateGenerator : ICertificateGenerator
{
    private readonly IRSAProvider _rsaProvider;


    public CertificateGenerator(IRSAProvider rsaProvider)
    {
        _rsaProvider = rsaProvider;
    }

    /// <summary>
    ///     Create a BouncyCastle AsymmetricCipherKeyPair and associated
    ///     X509Certificate
    ///     <remarks>
    ///         Based on:
    ///         http://stackoverflow.com/questions/3770233/is-it-possible-to-programmatically-generate-an-x509-certificate-using-only-c
    ///         http://web.archive.org/web/20100504192226/http://www.fkollmann.de/v2/post/Creating-certificates-using-BouncyCastle.aspx
    ///         requires http://www.bouncycastle.org/csharp/
    ///     </remarks>
    /// </summary>
    public (X509Certificate Certificate, AsymmetricCipherKeyPair KeyPair) GenerateSelfSignedCertificate(
        X509Name subjectName,
        int validDays,
        int keyLength,
        Action<X509V3CertificateGenerator>? configureGenerator = null)
    {

        var kp = _rsaProvider.CreateRSAKeyPair(keyLength);
        var serialNo = BigInteger.ProbablePrime(120, new Random());

        return (GenerateCertificate(
            kp,
            kp,
            subjectName,
            subjectName,
            serialNo,
            serialNo,
            DateTime.Now.AddDays(validDays),
            configureGenerator
        ), kp);
    }
        
    public (X509Certificate Certificate, AsymmetricCipherKeyPair KeyPair) 
        GenerateCertificate(
            AsymmetricCipherKeyPair issuerKeyPair,
            X509Certificate issuerCertificate,
            X509Name subjectName,
            DateTime notAfter,
            int keyLength,
                
            Action<X509V3CertificateGenerator>? configureGenerator = null)
    {
        var serialNo = BigInteger.ProbablePrime(120, new Random());
        var kp = _rsaProvider.CreateRSAKeyPair(keyLength);

        return (GenerateCertificate(
            kp,
            issuerKeyPair,
            subjectName,
            issuerCertificate.SubjectDN,
            serialNo,
            issuerCertificate.SerialNumber,
            notAfter > issuerCertificate.NotAfter ? issuerCertificate.NotAfter : notAfter,
            configureGenerator
        ), kp);
    }

    private static X509Certificate GenerateCertificate(
        AsymmetricCipherKeyPair subjectKeyPair,
        AsymmetricCipherKeyPair issuerKeyPair,
        X509Name subjectName,
        X509Name issuerName,
        BigInteger subjectSerialNo,
        BigInteger issuerSerialNo,
        DateTime notAfter,
        Action<X509V3CertificateGenerator>? configureGenerator
    )
    {
        var gen = new X509V3CertificateGenerator();
            
        gen.SetSerialNumber(subjectSerialNo);
        gen.SetSubjectDN(subjectName);
        gen.SetIssuerDN(issuerName);
        gen.SetNotAfter(notAfter);
        gen.SetNotBefore(DateTime.Now.Subtract(new TimeSpan(1, 0, 0, 0)));
        gen.SetPublicKey(subjectKeyPair.Public);

        gen.AddExtension(
            X509Extensions.AuthorityKeyIdentifier.Id,
            false,
            new AuthorityKeyIdentifier(
                SubjectPublicKeyInfoFactory.CreateSubjectPublicKeyInfo(issuerKeyPair.Public),
                new GeneralNames(new GeneralName(issuerName)),
                issuerSerialNo));
            
        var subjectKeyIdentifier =
            new SubjectKeyIdentifier(
                SubjectPublicKeyInfoFactory.CreateSubjectPublicKeyInfo(subjectKeyPair.Public));
        gen.AddExtension(
            X509Extensions.SubjectKeyIdentifier.Id, false, subjectKeyIdentifier);
        
        configureGenerator?.Invoke(gen);
            
        return gen.Generate(new Asn1SignatureFactory("SHA256withRSA", issuerKeyPair.Private));

    }
        
}