namespace Haipa.Messages
{
    public enum MessageOwner
    {
        VMAgent,
        TaskQueue,
        Controllers
    }

    public enum MessageSubscribers
    {
        ControllerModules,
        VMAgentModules,
        ApiModules,
        IdentityModules
    }
}