namespace Haipa.Messages.Events
{
    [SubscribeEvent(MessageSubscribers.VMAgentModules)]
    [Message(MessageOwner.Controllers)]
    public class InventoryRequestedEvent
    {

    }
}