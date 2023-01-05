using System;
using Org.BouncyCastle.Asn1.X509;

namespace Eryph.Runtime.Zero.HttpSys;

public class SSLOptions
{
    public SSLOptions(string issuer, string subject, DateTime validStartDate, uint validDays,
        string exportDirectory, string caFileName, Guid? appId = null, Uri url = null)
    {
        AppId = appId;
        Url = url;

        RootCertificate =
            new RootCertificateOptions(
                new X509Name("CN=" + issuer), exportDirectory, caFileName, validStartDate, validDays);
        Certificate = new CertificateOptions(new X509Name("CN=" + subject),
            subject, validStartDate, validDays);
    }

    public RootCertificateOptions RootCertificate { get; }
    public CertificateOptions Certificate { get; }
    public Guid? AppId { get; init; }
    public Uri Url { get; init; }
}