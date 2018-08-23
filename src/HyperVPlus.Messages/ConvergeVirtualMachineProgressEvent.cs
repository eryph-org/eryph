using System;

namespace HyperVPlus.Messages
{
    public class ConvergeVirtualMachineProgressEvent
    {
        public Guid CorellationId { get; set; }
        public string Message { get; set; }

    }
}