using System;
using Eryph.Resources.Machines;

namespace Eryph.Messages.Resources.Catlets.Events
{
    [SubscribesMessage(MessageSubscriber.Controllers)]
    public class VMStateChangedEvent
    {
        public Guid VmId { get; set; }
        public VmStatus Status { get; set; }
        public DateTime TimeStamp { get; set; }
    }
}