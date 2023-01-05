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

        public async Task<Operation?> StartNew(Guid tenantId, object command)
        {
            return (await StartOpOrTask(tenantId, Guid.Empty, Guid.Empty, command, null)).FirstOrDefault();
        }

        public async Task<Operation?> StartNew<T>(Guid tenantId, Resource resource = default)
            where T : class, new()
        {
            return (await StartOpOrTask(tenantId, Guid.Empty, Guid.Empty, Activator.CreateInstance<T>(), resource)).FirstOrDefault();
        }

        public Task<IEnumerable<Operation>> StartNew<T>(Guid tenantId, params Resource[] resources)
            where T : class, new()
        {
            return StartOpOrTask(tenantId, Guid.Empty, Guid.Empty,  Activator.CreateInstance<T>(), resources);
        }

        public async Task<Operation?> StartNew(Guid tenantId, Type commandType, Resource resource = default)
        {
            return (await StartNew(tenantId, commandType, new[] { resource }))?.FirstOrDefault();
        }

        public Task<IEnumerable<Operation>> StartNew(Guid tenantId, Type commandType, params Resource[] resources)
        {
            return StartOpOrTask(tenantId, Guid.Empty, Guid.Empty, Activator.CreateInstance(commandType) ?? throw new InvalidOperationException(), resources);
        }


        public Task<IEnumerable<Operation>> StartNew(Guid tenantId, object command, params Resource[] resources)
        {
            return StartOpOrTask(tenantId, Guid.Empty, Guid.Empty,  command, resources);

        }

    }
}