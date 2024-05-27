using System;
using System.Collections.Generic;
using Eryph.ConfigModel;
using Eryph.ConfigModel.Catlets;
using JetBrains.Annotations;

namespace Eryph.Resources.Machines
{
    // The change tracking in the controller module must be updated when modifying this class.
    public sealed class CatletMetadata
    {
        public Guid Id { get; set; }

        [PrivateIdentifier]
        public Guid VMId { get; set; }
        public Guid MachineId { get; set; }

        [CanBeNull] public string Parent { get; set; }

        [CanBeNull] public CatletConfig ParentConfig { get; set; }

        [CanBeNull] public FodderConfig[] Fodder { get; set; }
        public bool SecureDataHidden { get; set; }
    }
}