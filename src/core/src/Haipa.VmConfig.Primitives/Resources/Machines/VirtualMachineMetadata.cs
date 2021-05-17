using System;
using Haipa.Primitives.Resources.Machines.Config;
using JetBrains.Annotations;

namespace Haipa.Primitives.Resources.Machines
{
    public sealed class VirtualMachineMetadata
    {
        public Guid Id { get; set; }
        public Guid VMId { get; set; }
        public long MachineId { get; set; }

        [CanBeNull] public VirtualMachineConfig ImageConfig { get; set; }
        [CanBeNull] public VirtualMachineProvisioningConfig ProvisioningConfig { get; set; }

    }
}