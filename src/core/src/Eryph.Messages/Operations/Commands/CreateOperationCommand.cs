namespace Eryph.Messages.Operations.Commands
{
    [SendMessageTo(MessageRecipient.Controllers)]
    public class CreateOperationCommand
    {
        public CreateNewOperationTaskCommand TaskMessage { get; set; }
    }
}