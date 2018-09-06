using System;
using HyperVPlus.VmConfig;
using Newtonsoft.Json;

namespace HyperVPlus.Messages
{
    public class ConvergeVirtualMachineRequestedEvent
    {
        public Guid CorellationId { get; set; }
        public VirtualMachineConfig Config { get; set; }

    }
}