using System;
using System.Threading.Tasks;
using Haipa.Messages.Operations;
using Haipa.StateDb;
using Haipa.StateDb.Model;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Rebus.Handlers;
using Rebus.Retry.Simple;

namespace Haipa.Modules.Controller.Operations
{
    public class FailedOperationHandler<T> : IHandleMessages<IFailed<T>> where T: IOperationTaskMessage
    {
        private readonly IStateStoreRepository<Operation> _repository;
        private readonly IHostEnvironment _environment;
        private readonly ILogger<FailedOperationHandler<T>> _logger;

        public FailedOperationHandler(IStateStoreRepository<Operation> repository, IHostEnvironment environment, ILogger<FailedOperationHandler<T>> logger)
        {
            _repository = repository;
            _environment = environment;
            _logger = logger;
        }

        public async Task Handle(IFailed<T> failedMessage)
        {
            var operation = await _repository.GetByIdAsync(failedMessage.Message.OperationId);
            if (operation == null)
                return;

            operation.Status = OperationStatus.Failed;

            _logger.LogError($"Operation {operation.Id} failed with message: {failedMessage.ErrorDescription}");

            operation.StatusMessage = _environment.IsDevelopment() 
                ? failedMessage.ErrorDescription 
                : $"Internal error occurred. Failed TaskId: {failedMessage.Message.TaskId}";
            

            await _repository.UpdateAsync(operation);

        }
    }
}