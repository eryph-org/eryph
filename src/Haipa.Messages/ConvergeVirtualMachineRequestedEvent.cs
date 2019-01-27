using System;
using Haipa.VmConfig;

namespace Haipa.Messages
{
    public class ConvergeVirtualMachineRequestedEvent
    {
        public Guid CorellationId { get; set; }
        public MachineConfig Config { get; set; }

    }
}