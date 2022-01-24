using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using Eryph.Messages.Operations.Commands;
using Eryph.StateDb.Model;
using Resource = Eryph.Resources.Resource;

namespace Eryph.ModuleCore
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