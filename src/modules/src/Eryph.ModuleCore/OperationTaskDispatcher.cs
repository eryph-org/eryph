using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using Microsoft.Extensions.Logging;
using Rebus.Bus;
using Resource = Eryph.Resources.Resource;

namespace Eryph.ModuleCore;

public class OperationTaskDispatcher : OperationOrTaskDispatcherBase, IOperationTaskDispatcher
{

    public OperationTaskDispatcher(IBus bus, ILogger<OperationOrTaskDispatcherBase> logger, StateStoreContext db) : base(bus, logger, db)
    {
    }


    public async Task<Operation?> StartNew(Guid operationId, Guid initiatingTaskId, string traceId, object command)
    {
        if (operationId == Guid.Empty)
            throw new ArgumentException("Invalid empty operation id", nameof(operationId));

        if (initiatingTaskId == Guid.Empty)
            throw new ArgumentException("Invalid empty initiating task id", nameof(initiatingTaskId));


        return (await StartOpOrTask(Guid.Empty, operationId, initiatingTaskId, command,  traceId,null)).FirstOrDefault();
    }


    public async Task<Operation?> StartNew<T>(Guid operationId, Guid initiatingTaskId, string traceId, Resource resource = default)
        where T : class, new()
    {
        if (operationId == Guid.Empty)
            throw new ArgumentException("Invalid empty operation id", nameof(operationId));

        if (initiatingTaskId == Guid.Empty)
            throw new ArgumentException("Invalid empty initiating task id", nameof(initiatingTaskId));


        return (await StartOpOrTask(Guid.Empty, operationId, initiatingTaskId,Activator.CreateInstance<T>(), traceId, resource)).FirstOrDefault();
    }

    public Task<IEnumerable<Operation>> StartNew(Guid operationId, Guid initiatingTaskId, string traceId, object command, params Resource[] resources)
    {
        if (operationId == Guid.Empty)
            throw new ArgumentException("Invalid empty operation id", nameof(operationId));

        if (initiatingTaskId == Guid.Empty)
            throw new ArgumentException("Invalid empty initiating task id", nameof(initiatingTaskId));


        return StartOpOrTask(Guid.Empty, operationId, initiatingTaskId, command, traceId, resources);
    }


    public Task<IEnumerable<Operation>> StartNew<T>(Guid operationId, Guid initiatingTaskId, string traceId, [AllowNull] params Resource[] resources)
        where T : class, new()
    {
        if (operationId == Guid.Empty)
            throw new ArgumentException("Invalid empty operation id", nameof(operationId));

        if (initiatingTaskId == Guid.Empty)
            throw new ArgumentException("Invalid empty initiating task id", nameof(initiatingTaskId));


        return StartOpOrTask(Guid.Empty, operationId, initiatingTaskId, Activator.CreateInstance<T>(), traceId, resources);
    }

    public async Task<Operation?> StartNew(Guid operationId, Guid initiatingTaskId, string traceId, Type operationCommandType, Resource resource = default)
    {
        if (operationId == Guid.Empty)
            throw new ArgumentException("Invalid empty operation id", nameof(operationId));

        if (initiatingTaskId == Guid.Empty)
            throw new ArgumentException("Invalid empty initiating task id", nameof(initiatingTaskId));


        return (await StartNew(operationId, initiatingTaskId, traceId, operationCommandType, new[] { resource }))?.FirstOrDefault();
    }

    public Task<IEnumerable<Operation>> StartNew(Guid operationId, Guid initiatingTaskId, string traceId, Type commandType, [AllowNull] params Resource[] resources)
    {
        if (operationId == Guid.Empty)
            throw new ArgumentException("Invalid empty operation id", nameof(operationId));

        if (initiatingTaskId == Guid.Empty)
            throw new ArgumentException("Invalid empty initiating task id", nameof(initiatingTaskId));


        return StartOpOrTask(Guid.Empty, operationId, initiatingTaskId, Activator.CreateInstance(commandType), traceId, resources);
    }


}