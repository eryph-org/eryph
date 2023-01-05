using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Eryph.StateDb.Model;
using Resource = Eryph.Resources.Resource;

namespace Eryph.ModuleCore
{
    public interface IOperationTaskDispatcher
    {
        Task<Operation?> StartNew<T>(Guid operationId, Guid initiatingTaskId, Resource resource = default) where T : class, new();

        Task<IEnumerable<Operation>> StartNew<T>(Guid operationId, Guid initiatingTaskId, params Resource[] resources) where T : class, new();

        Task<Operation?> StartNew(Guid operationId, Guid initiatingTaskId, Type operationCommandType, Resource resource = default);
        Task<IEnumerable<Operation>> StartNew(Guid operationId, Guid initiatingTaskId, Type operationCommandType, params Resource[] resources);
        Task<Operation?> StartNew(Guid operationId, Guid initiatingTaskId, object command);

        Task<IEnumerable<Operation>> StartNew(Guid operationId, Guid initiatingTaskId, object command, params Resource[] resources);
    }

}