using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LanguageExt;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Eryph.ZeroState;
    
public interface IZeroStateInterceptor : IInterceptor
{
}

public abstract class ZeroStateInterceptorBase<TChange> : DbTransactionInterceptor
{
    private readonly IZeroStateQueue<TChange> _queue;
    private Option<ZeroStateQueueItem<TChange>> _currentItem = Option<ZeroStateQueueItem<TChange>>.None;

    protected ZeroStateInterceptorBase(
        IZeroStateQueue<TChange> queue)
    {
        _queue = queue;
    }

    protected abstract Task<Option<TChange>> DetectChanges(
        DbContext dbContext,
        CancellationToken cancellationToken = default);

    public override async ValueTask<InterceptionResult> TransactionCommittingAsync(
        DbTransaction transaction,
        TransactionEventData eventData,
        InterceptionResult result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is null)
            return await base.TransactionCommittingAsync(transaction, eventData, result, cancellationToken);

        _currentItem = await DetectChanges(eventData.Context, cancellationToken)
            .MapT(changes => new ZeroStateQueueItem<TChange>()
            {
                TransactionId = eventData.TransactionId,
                Changes = changes,
            });

        return await base.TransactionCommittingAsync(transaction, eventData, result, cancellationToken);
    }

    public override async Task TransactionCommittedAsync(
        DbTransaction transaction,
        TransactionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        await _currentItem.IfSomeAsync(item => _queue.EnqueueAsync(item, cancellationToken));
    }

    public override InterceptionResult TransactionCommitting(DbTransaction transaction, TransactionEventData eventData,
        InterceptionResult result)
    {
        throw new NotSupportedException();
    }

    public override void TransactionCommitted(DbTransaction transaction, TransactionEndEventData eventData)
    {
        throw new NotSupportedException();
    }
}
