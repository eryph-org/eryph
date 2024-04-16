using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Eryph.Modules.Controller.ChangeTracking
{
    public class ChangeTrackingConfig
    {
        public bool TrackChanges { get; set; }

        public bool SeedDatabase { get; set; }

        public string NetworksConfigPath { get; set; } = "";

        public string ProjectsConfigPath { get; set; } = "";

        public string ProjectNetworksConfigPath { get; set; } = "";

        public string ProjectNetworkPortsConfigPath { get; set; } = "";

        public string VirtualMachinesConfigPath { get; set; } = "";
    }
}
