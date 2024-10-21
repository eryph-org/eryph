using System;
using Eryph.Core.Genetics;
using Eryph.Resources.Machines;

namespace Eryph.Messages.Resources.Catlets.Commands
{
    public class ConvergeCatletResult
    {
        public Guid VmId { get; set; }

        public Guid MetadataId { get; set; }

        public VirtualMachineData Inventory { get; set; }

        public DateTimeOffset Timestamp { get; set; }

        public Architecture Architecture { get; set; }
    }
}