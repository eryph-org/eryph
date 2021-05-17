namespace Haipa.Messages.Operations.Events
{
    public class AcceptedOperationTaskEvent<T> where T : IOperationTaskMessage
    {
        public AcceptedOperationTaskEvent(T command)
        {
            Command = command;
        }

        public T Command { get; }
    }
}