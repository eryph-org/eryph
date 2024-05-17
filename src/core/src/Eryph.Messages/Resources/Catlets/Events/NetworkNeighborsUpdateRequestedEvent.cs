namespace Eryph.Messages.Resources.Catlets.Events;

[SubscribesMessage(MessageSubscriber.VMHostAgents)]
public class NetworkNeighborsUpdateRequestedEvent
{
    public NetworkNeighborRecord[] UpdatedAddresses { get; set; }
}