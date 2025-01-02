using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Eryph.Resources.Machines;

public class VirtualMachineSecurityData
{
    public bool TpmEnabled { get; init; }

    public bool KsdEnabled { get; init; }

    public bool Shielded { get; init; }

    public bool EncryptStateAndVmMigrationTraffic { get; init; }

    public bool VirtualizationBasedSecurityOptOut { get; init; }

    public bool BindToHostTpm { get; init; }
}
