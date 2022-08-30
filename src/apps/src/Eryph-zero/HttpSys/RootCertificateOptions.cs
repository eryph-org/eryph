using System;
using Org.BouncyCastle.Asn1.X509;

namespace Eryph.Runtime.Zero.HttpSys
{
    public record RootCertificateOptions(X509Name Name, string ExportDirectory, string FileName, DateTime ValidStartDate, uint ValidityDays);
}