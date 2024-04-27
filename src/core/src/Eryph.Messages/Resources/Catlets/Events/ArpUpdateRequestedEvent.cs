namespace Eryph.Messages.Resources.Catlets.Events;

[SubscribesMessage(MessageSubscriber.VMHostAgents)]
public class ArpUpdateRequestedEvent
{
    public string[] UpdatedAddresses { get; set; }
}