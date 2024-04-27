namespace Eryph.Messages.Resources.Catlets.Events;

[SubscribesMessage(MessageSubscriber.VMHostAgents)]
public class ArpUpdateRequestedEvent
{
    public ArpRecord[] UpdatedAddresses { get; set; }
}

public class ArpRecord
{
    public string IpAddress { get; set; }
    public string MacAddress { get; set; }
}