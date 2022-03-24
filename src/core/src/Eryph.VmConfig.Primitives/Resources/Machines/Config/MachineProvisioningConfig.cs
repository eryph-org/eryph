using Eryph.Core;
using Newtonsoft.Json.Linq;

namespace Eryph.Resources.Machines.Config
{
    public class MachineProvisioningConfig
    {
        [PrivateIdentifier] public string Hostname { get; set; }

        public CloudInitConfig[] Config { get; set; }
    }

}