using System;
using System.Collections.Generic;
using System.Text;

namespace Eryph.Configuration.Model
{
    public class CatletNetworkPortConfigModel
    {
        public string Name { get; set; }

        public string VirtualNetworkName { get; set; }

        public string EnvironmentName { get; set; }

        public string MacAddress { get; set; }

        public FloatingNetworkPortReferenceConfigModel FloatingNetworkPort { get; set; }

        public IpAssignmentConfigModel[] IpAssignments { get; set; }

        public Guid CatletMetadataId { get; set; }
    }
}
