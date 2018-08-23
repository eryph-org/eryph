using System;
using HyperVPlus.Messages;
using Rebus.Sagas;

namespace HyperVPlus.ConfigConsole
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

    }
}