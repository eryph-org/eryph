using System;
using System.Collections.Generic;
using System.Text;

namespace Eryph.Configuration.Model
{
    public class ProjectNetworkPortsConfig
    {
        public CatletNetworkPortConfigModel[] CatletNetworkPorts { get; set; } = Array.Empty<CatletNetworkPortConfigModel>();
    }
}
