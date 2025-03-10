using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Dbosoft.Rebus.Operations;
using Dbosoft.Rebus.Operations.Workflow;
using Eryph.Messages;
using Microsoft.Extensions.Logging;
using Rebus.Bus;

namespace Eryph.ModuleCore;

public class OperationDispatcher : DefaultOperationDispatcher
{
    private readonly IOperationManager _operationManager;

    public OperationDispatcher(
        IBus bus,
        WorkflowOptions options,
        ILogger<DefaultOperationDispatcher> logger,
        IOperationManager operationManager)
        : base(bus, options, logger, operationManager)
    {
        _operationManager = operationManager;
    }

    protected override async ValueTask<(IOperation, object)> CreateOperation(
        object command, 
        DateTimeOffset timestamp,
        object? additionalData,
        IDictionary<string,string>? additionalHeaders)
    {
        var operationId = Guid.NewGuid();

        if (command is IHasCorrelationId correlatedCommand)
        {
            operationId = correlatedCommand.CorrelationId == Guid.Empty
                ? operationId
                : correlatedCommand.CorrelationId;
        }

        var operation = await _operationManager.GetOrCreateAsync(
            operationId, command, timestamp, additionalData, additionalHeaders);

        return (operation, command);
    }
}
