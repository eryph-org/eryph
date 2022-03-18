using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using Eryph.StateDb.Model;
using Resource = Eryph.Resources.Resource;

namespace Eryph.ModuleCore
{
    public interface IOperationTaskDispatcher
    {
        Task<Operation?> StartNew<T>(Guid operationId, Resource resource = default) where T : class, new();

        Task<IEnumerable<Operation>> StartNew<T>(Guid operationId, params Resource[] resources) where T : class, new();

        Task<Operation?> StartNew(Guid operationId, Type operationCommandType, Resource resource = default);
        Task<IEnumerable<Operation>> StartNew(Guid operationId, Type operationCommandType, params Resource[] resources);
        Task<Operation?> StartNew(Guid operationId, object command);

        Task<IEnumerable<Operation>> StartNew(Guid operationId, object command, params Resource[] resources);
    }
}