using System;
using Org.BouncyCastle.Asn1.X509;

namespace Eryph.Runtime.Zero.HttpSys;

public record CertificateOptions(X509Name Name, string DnsName, DateTime ValidStartDate, uint ValidityDays);