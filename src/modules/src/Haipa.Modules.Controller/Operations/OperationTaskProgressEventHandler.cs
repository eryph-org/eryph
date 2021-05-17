using System.Threading.Tasks;
using Haipa.Messages.Operations.Events;
using Haipa.StateDb;
using Haipa.StateDb.Model;
using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Rebus.Handlers;

namespace Haipa.Modules.Controller.Operations
{
    [UsedImplicitly]
    public class OperationTaskProgressEventHandler : IHandleMessages<OperationTaskProgressEvent>
    {
        private readonly StateStoreContext _dbContext;
        private readonly ILogger<OperationTaskProgressEventHandler> _logger;

        public OperationTaskProgressEventHandler(StateStoreContext dbContext, ILogger<OperationTaskProgressEventHandler> logger)
        {
            _dbContext = dbContext;
            _logger = logger;
        }


        public async Task Handle(OperationTaskProgressEvent message)
        {
            _logger.LogDebug($"Received operation task progress event. Id : '{message.OperationId}/{message.TaskId}'");

            var operation = await _dbContext.Operations.FirstOrDefaultAsync(op => op.Id == message.OperationId);
            var task = await _dbContext.OperationTasks.FirstOrDefaultAsync(op => op.Id == message.TaskId);
            

            if (operation != null && task!=null)
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

                await _dbContext.AddAsync(opLogEntry);
            }
            else
            {
                _logger.LogWarning($"Received operation task progress event for a unknown operation task. Id : '{message.OperationId}/{message.TaskId}'", new
                {
                    message.OperationId,
                    message.TaskId,
                    message.Message,
                    message.Timestamp,
                });

            }
            
        }
    }
}