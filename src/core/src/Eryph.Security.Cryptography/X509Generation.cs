using System;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Operators;
using Org.BouncyCastle.Crypto.Prng;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.X509;

namespace Eryph.Security.Cryptography
{
    public static class X509Generation
    {
        /// <summary>
        ///     Create a BouncyCastle AsymmetricCipherKeyPair and associated
        ///     X509Certificate
        ///     <remarks>
        ///         Based on:
        ///         http://stackoverflow.com/questions/3770233/is-it-possible-to-programmatically-generate-an-x509-certificate-using-only-c
        ///         http://web.archive.org/web/20100504192226/http://www.fkollmann.de/v2/post/Creating-certificates-using-BouncyCastle.aspx
        ///         requires http://www.bouncycastle.org/csharp/
        ///     </remarks>
        ///     <param name="subjectName">
        ///         value assigned to CN field of the X.500 Distinguished Name
        ///         assigned to the certificate.
        ///         <remarks>
        ///             see http://msdn.microsoft.com/en-us/library/windows/desktop/aa366101(v=vs.85).aspx
        ///             for Distinguished Name format
        ///             See
        ///             http://stackoverflow.com/questions/5136198/what-strings-are-allowed-in-the-common-name-attribute-in-an-x-509-certificate
        ///             answer 2 for encoding details
        ///             Input is appended to "CN=".
        ///         </remarks>
        ///     </param>
        ///     <remarks>
        ///         Default is EmailProtection
        ///     </remarks>
        /// </summary>
        public static (X509Certificate Certificate, AsymmetricCipherKeyPair KeyPair) GenerateSelfSignedCertificate(
            string subjectName)
        {
            var kpGenerator = new RsaKeyPairGenerator();

            // certificate strength 2048 bits
            kpGenerator.Init(new KeyGenerationParameters(
                new SecureRandom(new CryptoApiRandomGenerator()), 2048));

            var kp = kpGenerator.GenerateKeyPair();

            var gen = new X509V3CertificateGenerator();

            var certName = new X509Name("CN=" + subjectName);
            var serialNo = BigInteger.ProbablePrime(120, new Random());

            gen.SetSerialNumber(serialNo);
            gen.SetSubjectDN(certName);
            gen.SetIssuerDN(certName);
            gen.SetNotAfter(DateTime.Now.AddYears(30));
            gen.SetNotBefore(DateTime.Now.Subtract(new TimeSpan(7, 0, 0, 0)));
            gen.SetPublicKey(kp.Public);

            gen.AddExtension(
                X509Extensions.AuthorityKeyIdentifier.Id,
                false,
                new AuthorityKeyIdentifier(
                    SubjectPublicKeyInfoFactory.CreateSubjectPublicKeyInfo(kp.Public),
                    new GeneralNames(new GeneralName(certName)),
                    serialNo));

            return (gen.Generate(new Asn1SignatureFactory("SHA256withRSA", kp.Private)), kp);
        }
    }
}