using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LanguageExt;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Eryph.Modules.Controller.ChangeTracking;

internal class ChangeTrackingDbTransactionInterceptor<TChange>(
    IChangeDetector<TChange> detector,
    IChangeTrackingQueue<TChange> queue,
    ILogger logger)
    : DbTransactionInterceptor
{
    private Seq<ChangeTrackingQueueItem<TChange>> _currentItem = Prelude.Empty;

    public override async ValueTask<InterceptionResult> TransactionCommittingAsync(
        DbTransaction transaction,
        TransactionEventData eventData,
        InterceptionResult result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is null)
            return await base.TransactionCommittingAsync(transaction, eventData, result, cancellationToken);

        _currentItem = await detector.DetectChanges(eventData.Context, cancellationToken)
            .MapT(changes => new ChangeTrackingQueueItem<TChange>(eventData.TransactionId, changes));

        return await base.TransactionCommittingAsync(transaction, eventData, result, cancellationToken);
    }

    public override async Task TransactionCommittedAsync(
        DbTransaction transaction,
        TransactionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        foreach (var item in _currentItem)
        {
            logger.LogDebug("Detected relevant changes in transaction {TransactionId}: {Changes}",
                item.TransactionId, item.Changes);
            await queue.EnqueueAsync(item, cancellationToken);
        }
    }

    public override InterceptionResult TransactionCommitting(
        DbTransaction transaction,
        TransactionEventData eventData,
        InterceptionResult result)
    {
        throw new NotSupportedException();
    }

    public override void TransactionCommitted(
        DbTransaction transaction,
        TransactionEndEventData eventData)
    {
        throw new NotSupportedException();
    }
}
