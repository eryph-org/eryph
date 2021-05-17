namespace Haipa.Messages.Operations.Commands
{
    [SendMessageTo(MessageRecipient.Controllers)]
    public class CreateOperationCommand
    {
        public CreateNewOperationTaskCommand TaskMessage { get; set; }
    }
}