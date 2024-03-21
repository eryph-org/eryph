using System;
using System.Collections.Generic;
using System.Text;

namespace Eryph.Configuration.Model
{
    public class VirtualNetworkConfigModel
    {
        public Guid Id { get; set; }

        public Guid ProjectId { get; set; }

        public string Name { get; set; }

        public string NetworkProvider { get; set; }

        public string IpNetwork { get; set; }

        public string Environment { get; set; }
    }
}
