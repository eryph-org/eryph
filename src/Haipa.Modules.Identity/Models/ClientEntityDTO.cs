using System;
using System.ComponentModel.DataAnnotations;

namespace Haipa.Modules.Identity.Models
{
   public class ClientEntityDTO
    {
        [Key]
        public string ClientId { get; set; }
        public string Description { get; set; }
        public string X509CertificateBase64 { get; set; }
        public string[] AllowedScopes { get; set; }
    }
}
