using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Haipa.StateDb.Model
{
    public class Network : Resource
    {

        public ulong VLanId { get; set; }

        public virtual List<Subnet> Subnets { get; set; }
    }
}