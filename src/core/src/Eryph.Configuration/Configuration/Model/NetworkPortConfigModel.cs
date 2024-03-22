using System;
using System.Collections.Generic;
using System.Text;

namespace Eryph.Configuration.Model
{
    internal class NetworkPortConfigModel
    {
        public string Name { get; set; }

        public IpAssignmentConfigModel[] IpAssignments { get; set; }


        public string ProviderName { get; set; }

        public string SubnetName { get; set; }

        public string VirtualNetworkName { get; set; }
    }
}
