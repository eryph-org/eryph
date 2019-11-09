using IdentityServer4.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.IO;
using System.Text;

namespace Haipa.IdentityDb.Models
{
    public class ClientEntity
    {
        public string ClientData { get; set; }
        [Key]
        public Guid ClientId { get; set; }
        [NotMapped]
        public Client Client { get; set; }
        public string ConfigFile { get; set; }
     
      
    }
}
