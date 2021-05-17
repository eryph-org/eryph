using System;
using Haipa.Resources.Machines;

namespace Haipa.Messages.Resources.Machines.Events
{
    [SubscribesMessage(MessageSubscriber.Controllers)]
    public class VMStateChangedEvent
    {
        public Guid VmId { get; set; }
        public VmStatus Status { get; set; }
    }
}