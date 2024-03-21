using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Eryph.Runtime.Zero.ZeroState;
using Eryph.StateDb.Model;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Eryph.StateDb
{
    public class ZeroStateDbTransactionInterceptor : DbTransactionInterceptor
    {
        private readonly ILogger<ZeroStateDbTransactionInterceptor> _logger;
        private readonly IZeroStateChannel<ZeroStateChangeSet> _channel;

        public ZeroStateDbTransactionInterceptor(
            ILogger<ZeroStateDbTransactionInterceptor> logger,
            IZeroStateChannel<ZeroStateChangeSet> channel)
        {
            _logger = logger;
            _channel = channel;
        }

        public override ValueTask<InterceptionResult> TransactionCommittingAsync(
            DbTransaction transaction,
            TransactionEventData eventData,
            InterceptionResult result,
            CancellationToken cancellationToken = default)
        {
            var changes = eventData.Context.ChangeTracker.Entries<VirtualNetwork>();
            

            return base.TransactionCommittingAsync(transaction, eventData, result, cancellationToken);
        }

        public override async Task TransactionCommittedAsync(
            DbTransaction transaction,
            TransactionEndEventData eventData,
            CancellationToken cancellationToken = default)
        {
            var entries = eventData.Context.ChangeTracker.Entries<VirtualNetwork>();
            if (!entries.Any())
                return;

            _logger.LogError("Detected change to relevant to zero state in transaction {TransactionId}", eventData.TransactionId);
            var changeSet = new ZeroStateChangeSet
            {
                TransactionId = eventData.TransactionId,
                Changes = eventData.Context.ChangeTracker.Entries<VirtualNetwork>()
                    .Select(e => new ZeroStateChange
                    {
                        Id = e.Entity.Id,
                        EntityType = typeof(VirtualNetwork)
                    }).ToList()
            };
            
            await _channel.WriteAsync(changeSet, cancellationToken);
        }
    }
}
