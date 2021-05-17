using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Haipa.StateDb.Model
{
    public class Network
    {
        public Guid Id { get; set; }

        [Required]
        public string Name { get; set; }

        public ulong VLanId { get; set; }

        public virtual List<Subnet> Subnets { get; set; }

    }
}