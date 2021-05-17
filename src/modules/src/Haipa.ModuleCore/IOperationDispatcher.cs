using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using Haipa.Messages.Operations.Commands;
using Haipa.StateDb.Model;
using Resource = Haipa.Resources.Resource;

namespace Haipa.ModuleCore
{
    public interface IOperationDispatcher
    {
        Task<Operation?> StartNew<T>(Resource resource = default) where T : class, new();

        Task<IEnumerable<Operation>> StartNew<T>([AllowNull] params Resource[] resources) where T : class, new();

        Task<Operation?> StartNew(Type commandType, Resource resource = default);
        Task<IEnumerable<Operation>> StartNew(Type commandType, [AllowNull] params Resource[] resources);
        Task<Operation?> StartNew(object operationCommand);

        Task<IEnumerable<Operation>> StartNew(object command,
            [AllowNull] params Resource[] resources);
    }
}