namespace Haipa.Messages.Operations
{

    public class AcceptedOperationTask<T> where T : IOperationTaskMessage
    {
        public T Command { get; }

        public AcceptedOperationTask(T command)
        {
            Command = command;
        }
    }
}