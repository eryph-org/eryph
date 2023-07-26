using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Eryph.StateDb.Model;
using Resource = Eryph.Resources.Resource;

namespace Eryph.ModuleCore
{
    public interface IOperationTaskDispatcher
    {
        Task<Operation?> StartNew<T>(Guid operationId, Guid initiatingTaskId, string traceId, Resource resource = default) where T : class, new();

        Task<IEnumerable<Operation>> StartNew<T>(Guid operationId, Guid initiatingTaskId, string traceId, params Resource[] resources) where T : class, new();

        Task<Operation?> StartNew(Guid operationId, Guid initiatingTaskId, string traceId, Type operationCommandType, Resource resource = default);
        Task<IEnumerable<Operation>> StartNew(Guid operationId, Guid initiatingTaskId, string traceId, Type operationCommandType, params Resource[] resources);
        Task<Operation?> StartNew(Guid operationId, Guid initiatingTaskId, string traceId, object command);

        Task<IEnumerable<Operation>> StartNew(Guid operationId, Guid initiatingTaskId, string traceId, object command, params Resource[] resources);
    }

}