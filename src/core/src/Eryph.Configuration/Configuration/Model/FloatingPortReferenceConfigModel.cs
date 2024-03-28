using System;
using System.Collections.Generic;
using System.Text;

namespace Eryph.Configuration.Model
{
    public class FloatingPortReferenceConfigModel
    {
        public string Name { get; set; }

        public string ProviderName { get; set; }

        // TODO do we need the subnet name?
        public string SubnetName { get; set; }
    }
}
