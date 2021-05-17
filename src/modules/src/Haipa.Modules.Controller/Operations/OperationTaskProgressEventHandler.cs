using System;
using System.Linq;
using System.Threading.Tasks;
using Haipa.Messages.Operations;
using Haipa.Messages.Operations.Events;
using Haipa.StateDb;
using Haipa.StateDb.Model;
using JetBrains.Annotations;
using Rebus.Handlers;

namespace Haipa.Modules.Controller.Operations
{
    [UsedImplicitly]
    public class OperationTaskProgressEventHandler : IHandleMessages<OperationTaskProgressEvent>
    {

        private readonly StateStoreContext _dbContext;

        public OperationTaskProgressEventHandler(StateStoreContext dbContext)
        {
            _dbContext = dbContext;
        }


        public Task Handle(OperationTaskProgressEvent message)
        {
            var operation = _dbContext.Operations.FirstOrDefault(op => op.Id == message.OperationId);
            var task = _dbContext.OperationTasks.FirstOrDefault(op => op.Id == message.TaskId);


            if (operation != null)
            {

                var opLogEntry =
                    new OperationLogEntry
                    {
                        Id = message.Id,
                        Message = message.Message,
                        Operation = operation,
                        Task = task,
                        Timestamp = message.Timestamp
                    };

                _dbContext.Add(opLogEntry);
            }

            Console.WriteLine(message.Message);
            return Task.CompletedTask;
        }

    }
}