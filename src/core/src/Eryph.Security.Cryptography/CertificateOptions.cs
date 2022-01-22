using System;

namespace Eryph.Security.Cryptography
{
    public class CertificateOptions
    {
        public string Issuer { get; set; }
        public string FriendlyName { get; set; }
        public string Suffix { get; set; }
        public DateTime ValidStartDate { get; set; }
        public DateTime ValidEndDate { get; set; }
        public string Password { get; set; }
        public string ExportDirectory { get; set; }
        public string AppID { get; set; }
        public string Thumbprint { get; set; }
        public string URL { get; set; }
        public string CACertName { get; set; }

        public string CAFilePath => ExportDirectory + CACertName;
    }
}