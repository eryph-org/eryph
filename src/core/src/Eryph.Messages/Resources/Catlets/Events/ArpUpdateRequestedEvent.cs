namespace Eryph.Messages.Resources.Catlets.Events;

[SubscribesMessage(MessageSubscriber.VMHostAgents)]
public class ArpUpdateRequestedEvent
{
    public ArpRecord[] UpdatedAddresses { get; set; }
}