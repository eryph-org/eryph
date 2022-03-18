using Eryph.Core;
using Newtonsoft.Json.Linq;

namespace Eryph.Resources.Machines.Config
{
    public class VirtualMachineProvisioningConfig
    {
        [PrivateIdentifier]
        public string Hostname { get; set; }

        [PrivateIdentifier(Critical = true)]
        public JObject UserData { get; set; }

        public ProvisioningMethod? Method { get; set; }
    }
}