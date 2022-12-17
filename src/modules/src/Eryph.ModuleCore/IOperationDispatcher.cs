using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Eryph.StateDb.Model;
using Resource = Eryph.Resources.Resource;

namespace Eryph.ModuleCore
{
    public interface IOperationDispatcher
    {
        Task<Operation?> StartNew<T>(Resource resource = default) where T : class, new();

        Task<IEnumerable<Operation>> StartNew<T>(params Resource[] resources) where T : class, new();

        Task<Operation?> StartNew(Type commandType, Resource resource = default);
        Task<IEnumerable<Operation>> StartNew(Type commandType, params Resource[] resources);
        Task<Operation?> StartNew(object operationCommand);

        Task<IEnumerable<Operation>> StartNew(object command, params Resource[] resources);
    }
}