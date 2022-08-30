using System;
using Eryph.ConfigModel.Machine;
using Eryph.Core;
using JetBrains.Annotations;

namespace Eryph.Resources.Machines
{
    public sealed class VirtualMachineMetadata
    {
        public Guid Id { get; set; }

        [PrivateIdentifier]
        public Guid VMId { get; set; }
        public Guid MachineId { get; set; }

        [CanBeNull] public VirtualMachineConfig ImageConfig { get; set; }
        [CanBeNull] public MachineProvisioningConfig ProvisioningConfig { get; set; }
        public bool SensitiveDataHidden { get; set; }
    }
}