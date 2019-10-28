using IdentityServer4.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace Haipa.IdentityDb.Models
{
    public class IdentityResourceEntity
    {
        public string IdentityResourceData { get; set; }

        [Key]
        public string IdentityResourceName { get; set; }

        [NotMapped]
        public IdentityResource IdentityResource { get; set; }

       
    }
}
