namespace Haipa.Messages.Operations
{
    [SendMessageTo(MessageRecipient.Controllers)]
    public class CreateOperationCommand
    {
        public CreateNewOperationTaskCommand TaskMessage { get; set; }
    }
}