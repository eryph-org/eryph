using System;

namespace Haipa.Messages
{
    public class ConvergeVirtualMachineProgressEvent
    {
        public Guid OperationId { get; set; }
        public string Message { get; set; }

    }

}