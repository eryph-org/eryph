using System;
using System.Collections.Generic;

namespace Eryph.StateDb.Model
{
    public class VirtualNetworkPort
    {
        public Guid Id { get; set; }

        public string MacAddress { get; set; }

        public Guid NetworkId { get; set; }
        public virtual VirtualNetwork Network { get; set; }

        public string Name { get; set; }

        public virtual List<IpAssignment> IpAssignments { get; set; }
    }
}