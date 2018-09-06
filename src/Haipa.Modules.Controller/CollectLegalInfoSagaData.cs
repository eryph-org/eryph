using System;
using HyperVPlus.VmConfig;
using Rebus.Sagas;

namespace Haipa.Modules.Controller
{
    public class CollectLegalInfoSagaData : ISagaData
    {
        // these two are required by Rebus
        public Guid Id { get; set; }
        public int Revision { get; set; }

        // add your own fields and objects down here:
        public Guid CorrelationId { get; set; }
        public VirtualMachineConfig Config { get; set; }

        public Guid VirtualMaschineId { get; set; }
        public string AgentName { get; set; }

        public bool InventoryReceived { get; set; }

    }
}