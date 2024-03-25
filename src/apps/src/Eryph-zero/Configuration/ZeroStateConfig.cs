using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.ZeroState;

namespace Eryph.Runtime.Zero.Configuration
{
    internal class ZeroStateConfig : IZeroStateConfig
    {
        public string ProjectsConfigPath => ZeroConfig.GetProjectsConfigPath();

        public string ProjectNetworksConfigPath => ZeroConfig.GetProjectNetworksConfigPath();
        public string NetworkPortsConfigPath => ZeroConfig.GetNetworkPortsConfigPath();
    }
}
