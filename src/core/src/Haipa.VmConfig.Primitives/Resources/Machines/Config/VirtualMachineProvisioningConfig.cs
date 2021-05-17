using Newtonsoft.Json.Linq;

namespace Haipa.Resources.Machines.Config
{
    public class VirtualMachineProvisioningConfig
    {
        public string Hostname { get; set; }

        public JObject UserData { get; set; }

        public ProvisioningMethod Method { get; set; }
    }
}