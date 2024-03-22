using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.StateDb.Model;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Eryph.ZeroState
{
    /*
    public class ZeroStateInterceptor : DbTransactionInterceptor
    {
        private readonly ILogger<ZeroStateInterceptor> _logger;
        private readonly IZeroStateQueue _queue;

        public ZeroStateInterceptor(
            ILogger<ZeroStateInterceptor> logger,
            IZeroStateQueue queue)
        {
            _logger = logger;
            _queue = queue;
        }

        public override async ValueTask<InterceptionResult> TransactionCommittingAsync(
            DbTransaction transaction,
            TransactionEventData eventData,
            InterceptionResult result,
            CancellationToken cancellationToken = default)
        {
            // Any access to the database needs to happen before the transaction is committed
            foreach (var subnetEntry in eventData.Context.ChangeTracker.Entries<VirtualNetworkSubnet>())
            {
                await subnetEntry.Reference(s => s.Network).LoadAsync(cancellationToken);
                var projectId = subnetEntry.Entity.Network.ProjectId;
            }

            return await base.TransactionCommittingAsync(transaction, eventData, result, cancellationToken);
        }

        public override async Task TransactionCommittedAsync(
            DbTransaction transaction,
            TransactionEndEventData eventData,
            CancellationToken cancellationToken = default)
        {
            _logger.LogError("Detected change to relevant to zero state in transaction {TransactionId}",
                eventData.TransactionId);
            var changeSet = new ZeroStateQueueItem()
            {
                TransactionId = eventData.TransactionId,
                Changes = eventData.Context.ChangeTracker.Entries<VirtualNetwork>()
                    .Select(e => new ZeroStateChange
                    {
                        Id = e.Entity.Id,
                        EntityType = typeof(VirtualNetwork)
                    }).Concat(eventData.Context.ChangeTracker.Entries<FloatingNetworkPort>()
                        .Select(e => new ZeroStateChange()
                        {

                            Id = e.Entity.Id,
                            EntityType = typeof(FloatingNetworkPort)
                        }))
                    .Concat(eventData.Context.ChangeTracker.Entries<CatletNetworkPort>()
                        .Select(e => new ZeroStateChange()
                        {
                            Id = e.Entity.Id,
                            EntityType = typeof(CatletNetworkPort)
                        }))
                    .ToList()
            };

            foreach (var subnetEntry in eventData.Context.ChangeTracker.Entries<VirtualNetworkSubnet>())
            {
                await subnetEntry.Reference(s => s.Network).LoadAsync(cancellationToken);
                var projectId = subnetEntry.Entity.Network.ProjectId;
            }


            if (!changeSet.Changes.Any())
                return;

            await _queue.EnqueueAsync(changeSet, cancellationToken);
        }
    }
    */
}
