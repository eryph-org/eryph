using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using Haipa.Messages.Operations.Commands;
using Haipa.StateDb.Model;
using Resource = Haipa.Resources.Resource;

namespace Haipa.ModuleCore
{
    public interface IOperationTaskDispatcher
    {
        Task<Operation?> StartNew<T>(Guid operationId, Resource resource = default) where T : class, new();

        Task<IEnumerable<Operation>> StartNew<T>(Guid operationId, [AllowNull] params Resource[] resources) where T : class, new();

        Task<Operation?> StartNew(Guid operationId, Type operationCommandType, Resource resource = default);
        Task<IEnumerable<Operation>> StartNew(Guid operationId, Type operationCommandType, [AllowNull] params Resource[] resources);
        Task<Operation?> StartNew(Guid operationId, object command);

        Task<IEnumerable<Operation>> StartNew(Guid operationId, object command,
            [AllowNull] params Resource[] resources);
    }
}