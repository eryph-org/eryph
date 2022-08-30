using System;
using Org.BouncyCastle.Asn1.X509;

namespace Eryph.Security.Cryptography;

public record RootCertificateOptions(X509Name Name, string ExportDirectory, string FileName, DateTime ValidStartDate, uint ValidityDays);