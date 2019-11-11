using System.ComponentModel.DataAnnotations;

namespace Haipa.Modules.Identity.Models.V1
{
   public class ClientApiModel : IClientApiModel
    {
        [Key]
        [Required]
        [RegularExpression("^[a-zA-Z0-9-_]*$")]
        [MaxLength(20)]
        public string ClientId { get; set; }

        [MaxLength(40)]
        public string Description { get; set; }

        public string X509CertificateBase64 { get; set; }
        public string[] AllowedScopes { get; set; }
    }
}
