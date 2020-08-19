using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace Haipa.Modules.Identity.Models.V1
{
    [DisplayName("Clients")]
    public class ClientApiModel : IClientApiModel
    {
        /// <summary>
        /// Unique identifier for a haipa client
        /// Only characters a-z, A-Z, numbers 0-9 and hyphens are allowed.
        /// </summary>
        [Key]
        [MaxLength(36)]
        public string Id { get; set; }

        /// <summary>
        /// human readable name of client
        /// </summary>
        [MaxLength(20)]
        public string Name { get; set; }

        /// <summary>
        /// optional description of client
        /// </summary>
        [MaxLength(40)]
        public string Description { get; set; }

        /// <summary>
        /// The clients public certificate (base64 encoded)
        /// </summary>
        public string Certificate { get; set; }

        /// <summary>
        /// allowed scopes of client
        /// </summary>
        public string[] AllowedScopes { get; set; }

    }
}
