using System;
using Eryph.Resources.Machines.Config;
using JetBrains.Annotations;

namespace Eryph.Resources.Machines
{
    public sealed class VirtualMachineMetadata
    {
        public Guid Id { get; set; }
        public Guid VMId { get; set; }
        public Guid MachineId { get; set; }

        [CanBeNull] public VirtualMachineConfig ImageConfig { get; set; }
        [CanBeNull] public VirtualMachineProvisioningConfig ProvisioningConfig { get; set; }
    }
}