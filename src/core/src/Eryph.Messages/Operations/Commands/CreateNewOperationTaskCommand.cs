using System;

namespace Eryph.Messages.Operations.Commands
{
    [SendMessageTo(MessageRecipient.Controllers)]
    public class CreateNewOperationTaskCommand : IOperationTaskMessage
    {
        // ReSharper disable once UnusedMember.Global
        public CreateNewOperationTaskCommand()
        {
        }

        public CreateNewOperationTaskCommand(string commandType, string commandData, 
            Guid operationId, Guid initiatingTaskId, Guid taskId)
        {
            CommandType = commandType;
            CommandData = commandData;
            OperationId = operationId;
            InitiatingTaskId = initiatingTaskId;
            TaskId = taskId;
        }

        public string CommandData { get; set; }
        public string CommandType { get; set; }

        public Guid OperationId { get; set; }
        public Guid InitiatingTaskId { get; set; }
        public Guid TaskId { get; set; }
    }
}