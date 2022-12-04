using System;
using Eryph.ConfigModel.Catlets;
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

        [CanBeNull] public VirtualCatletConfig ImageConfig { get; set; }
        [CanBeNull] public CatletRaisingConfig RaisingConfig { get; set; }
        public bool SensitiveDataHidden { get; set; }
    }
}