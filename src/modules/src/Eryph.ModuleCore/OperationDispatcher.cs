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

        public async Task<Operation?> StartNew(Guid tenantId, string traceId, object command)
        {
            return (await StartOpOrTask(tenantId, Guid.Empty, Guid.Empty, command, traceId, null)).FirstOrDefault();
        }

        public async Task<Operation?> StartNew<T>(Guid tenantId, string traceId, Resource resource = default)
            where T : class, new()
        {
            return (await StartOpOrTask(tenantId, Guid.Empty, Guid.Empty, Activator.CreateInstance<T>(), traceId, resource)).FirstOrDefault();
        }

        public Task<IEnumerable<Operation>> StartNew<T>(Guid tenantId, string traceId, params Resource[] resources)
            where T : class, new()
        {
            return StartOpOrTask(tenantId, Guid.Empty, Guid.Empty,  Activator.CreateInstance<T>(), traceId, resources);
        }

        public async Task<Operation?> StartNew(Guid tenantId, string traceId, Type commandType, Resource resource = default)
        {
            return (await StartNew(tenantId, traceId, commandType, new[] { resource }))?.FirstOrDefault();
        }

        public Task<IEnumerable<Operation>> StartNew(Guid tenantId, string traceId, Type commandType, params Resource[] resources)
        {
            return StartOpOrTask(tenantId, Guid.Empty, Guid.Empty, Activator.CreateInstance(commandType) ?? throw new InvalidOperationException(), traceId, resources);
        }


        public Task<IEnumerable<Operation>> StartNew(Guid tenantId, string traceId, object command, params Resource[] resources)
        {
            return StartOpOrTask(tenantId, Guid.Empty, Guid.Empty,  command, traceId, resources);

        }

    }
}