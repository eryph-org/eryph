namespace Haipa.Messages.Operations.Events
{

    public class AcceptedOperationTaskEvent<T> where T : IOperationTaskMessage
    {
        public T Command { get; }

        public AcceptedOperationTaskEvent(T command)
        {
            Command = command;
        }
    }
}