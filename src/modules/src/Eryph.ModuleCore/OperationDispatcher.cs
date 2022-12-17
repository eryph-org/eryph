using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using Microsoft.Extensions.Logging;
using Rebus.Bus;
using Resource = Eryph.Resources.Resource;

namespace Eryph.ModuleCore
{
    public class OperationDispatcher : OperationOrTaskDispatcherBase, IOperationDispatcher
    {
        public OperationDispatcher(IBus bus, ILogger<OperationOrTaskDispatcherBase> logger, StateStoreContext db) : base(bus, logger, db)
        {
        }

        public async Task<Operation?> StartNew(object command)
        {
            return (await StartOpOrTask(Guid.Empty, Guid.Empty, command, null)).FirstOrDefault();
        }

        public async Task<Operation?> StartNew<T>(Resource resource = default)
            where T : class, new()
        {
            return (await StartOpOrTask(Guid.Empty, Guid.Empty, Activator.CreateInstance<T>(), resource)).FirstOrDefault();
        }

        public Task<IEnumerable<Operation>> StartNew<T>(params Resource[] resources)
            where T : class, new()
        {
            return StartOpOrTask(Guid.Empty, Guid.Empty,  Activator.CreateInstance<T>(), resources);
        }

        public async Task<Operation?> StartNew(Type commandType, Resource resource = default)
        {
            return (await StartNew(commandType, new[] { resource }))?.FirstOrDefault();
        }

        public Task<IEnumerable<Operation>> StartNew(Type commandType, params Resource[] resources)
        {
            return StartOpOrTask(Guid.Empty, Guid.Empty, Activator.CreateInstance(commandType) ?? throw new InvalidOperationException(), resources);
        }


        public Task<IEnumerable<Operation>> StartNew(object command, params Resource[] resources)
        {
            return StartOpOrTask(Guid.Empty, Guid.Empty,  command, resources);

        }

    }
}