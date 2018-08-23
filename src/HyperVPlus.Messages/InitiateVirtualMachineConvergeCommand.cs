using System;
using HyperVPlus.VmConfig;

namespace HyperVPlus.Messages
{
    public class InitiateVirtualMachineConvergeCommand
    {
        public ConfigEntry Config { get; set; }
        public Guid ConvergeProcessId { get; set; }
    }
}