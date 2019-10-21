namespace Haipa.Messages.Operations
{
    [Message(MessageOwner.Controllers)]
    public class CreateOperationCommand
    {
        public CreateNewOperationTaskCommand TaskMessage { get; set; }
    }
}