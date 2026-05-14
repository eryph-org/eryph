using System;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using LanguageExt;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Eryph.Modules.Controller.ChangeTracking;

/// <summary>
/// Base class for change interceptors that detect changes in the database.
/// </summary>
/// <remarks>
/// We use both <see cref="CreatedSavepointAsync(DbTransaction, TransactionEventData, CancellationToken)"/>
/// and <see cref="TransactionCommittingAsync(DbTransaction, TransactionEventData, InterceptionResult, CancellationToken)"/>
/// to detect changes. EF Core will implicitly create a savepoint when
/// <c>SaveChangesAsync()</c> is called. We must detect changes when a savepoint
/// is created. Otherwise, we would miss deleted entities as EF Core seems
/// to remove them from the change tracker after the <c>SaveChangesAsync()</c>.
/// </remarks>
internal abstract class ChangeInterceptorBase<TChange> : DbTransactionInterceptor
{
    private readonly IChangeTrackingQueue<TChange> _queue;
    private readonly ILogger _logger;
    private HashSet<ChangeTrackingQueueItem<TChange>> _changes = HashSet<ChangeTrackingQueueItem<TChange>>.Empty;

    protected ChangeInterceptorBase(
        IChangeTrackingQueue<TChange> queue,
        ILogger logger)
    {
        _queue = queue;
        _logger = logger;
    }

    protected abstract Task<Seq<TChange>> DetectChanges(
        DbContext dbContext,
        CancellationToken cancellationToken = default);

    public override async Task CreatedSavepointAsync(
        DbTransaction transaction,
        TransactionEventData eventData,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is null)
        {
            await base.CreatedSavepointAsync(transaction, eventData, cancellationToken);
            return;
        }

        var currentChanges = await DetectChanges(eventData.Context, cancellationToken)
            .MapT(changes => new ChangeTrackingQueueItem<TChange>(eventData.TransactionId, changes));

        _changes = _changes.Union(currentChanges);

        await base.CreatedSavepointAsync(transaction, eventData, cancellationToken);
    }

    public override async ValueTask<InterceptionResult> TransactionCommittingAsync(
        DbTransaction transaction,
        TransactionEventData eventData,
        InterceptionResult result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is null)
            return await base.TransactionCommittingAsync(transaction, eventData, result, cancellationToken);

        var currentChanges = await DetectChanges(eventData.Context, cancellationToken)
            .MapT(changes => new ChangeTrackingQueueItem<TChange>(eventData.TransactionId, changes));

        _changes = _changes.Union(currentChanges);

        return await base.TransactionCommittingAsync(transaction, eventData, result, cancellationToken);
    }

    public override async Task TransactionCommittedAsync(
        DbTransaction transaction,
        TransactionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        foreach (var item in _changes)
        {
            _logger.LogDebug("Detected relevant changes in transaction {TransactionId}: {Changes}",
                item.TransactionId, item.Changes);
            await _queue.EnqueueAsync(item, cancellationToken);
        }
    }

    public override void CreatedSavepoint(
        DbTransaction transaction,
        TransactionEventData eventData)
    {
        throw new NotSupportedException();
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
