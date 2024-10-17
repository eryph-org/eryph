using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.Core.Genetics;

namespace Eryph.Modules.VmHostAgent.Inventory;

public class HostArchitectureProvider : IHostArchitectureProvider
{
    public GeneArchitecture Architecture =>
        // Currently, we only support Hyper-V on AMD64 and can hardcode the value here.
        GeneArchitecture.New("hyperv/amd64");
}
