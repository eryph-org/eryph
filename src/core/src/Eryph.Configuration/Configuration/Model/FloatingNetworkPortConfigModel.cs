using System;
using System.Collections.Generic;
using System.Text;

namespace Eryph.Configuration.Model
{
    public class FloatingNetworkPortConfigModel
    {
        public string Name { get; set; }

        public string ProviderName { get; set; }

        public string SubnetName { get; set; }

        public string PoolName { get; set; }

        public IpAssignmentConfigModel[] IpAssignments { get; set; }
    }
}
