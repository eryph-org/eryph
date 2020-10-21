using System;
using JetBrains.Annotations;

namespace Haipa.VmConfig
{
    public sealed class VirtualMachineMetadata
    {
        public Guid Id { get; set; }
        public Guid VMId { get; set; }
        [CanBeNull] public VirtualMachineConfig ImageConfig { get; set; }
        [CanBeNull] public VirtualMachineProvisioningConfig ProvisioningConfig { get; set; }

    }
}