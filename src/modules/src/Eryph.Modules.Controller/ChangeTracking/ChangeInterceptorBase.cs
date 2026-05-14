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
        _logger.LogWarning("CTDIAG savepoint {TChange} ctx={Ctx} tx={Tx}", typeof(TChange).Name, eventData.Context is not null, eventData.TransactionId);
        if (eventData.Context is null)
        {
            await base.CreatedSavepointAsync(transaction, eventData, cancellationToken);
            return;
        }

        var detected = await DetectChanges(eventData.Context, cancellationToken);
        _logger.LogWarning("CTDIAG savepoint detected {Count} for {TChange} -- entries={Entries}", detected.Count, typeof(TChange).Name, string.Join(",", detected));
        var currentChanges = detected
            .Map(changes => new ChangeTrackingQueueItem<TChange>(eventData.TransactionId, changes));

        _changes = _changes.Union(currentChanges);

        await base.CreatedSavepointAsync(transaction, eventData, cancellationToken);
    }

    public override async ValueTask<InterceptionResult> TransactionCommittingAsync(
        DbTransaction transaction,
        TransactionEventData eventData,
        InterceptionResult result,
        CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("CTDIAG committing {TChange} ctx={Ctx} tx={Tx}", typeof(TChange).Name, eventData.Context is not null, eventData.TransactionId);
        if (eventData.Context is null)
            return await base.TransactionCommittingAsync(transaction, eventData, result, cancellationToken);

        var detected = await DetectChanges(eventData.Context, cancellationToken);
        _logger.LogWarning("CTDIAG committing detected {Count} for {TChange} -- entries={Entries}", detected.Count, typeof(TChange).Name, string.Join(",", detected));
        var currentChanges = detected
            .Map(changes => new ChangeTrackingQueueItem<TChange>(eventData.TransactionId, changes));

        _changes = _changes.Union(currentChanges);

        return await base.TransactionCommittingAsync(transaction, eventData, result, cancellationToken);
    }

    public override async Task TransactionCommittedAsync(
        DbTransaction transaction,
        TransactionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("CTDIAG committed {TChange} _changes={Count} tx={Tx}", typeof(TChange).Name, _changes.Count, eventData.TransactionId);
        foreach (var item in _changes)
        {
            _logger.LogWarning("CTDIAG enqueueing {TChange} tx={Tx} change={Change}", typeof(TChange).Name, item.TransactionId, item.Changes);
            await _queue.EnqueueAsync(item, cancellationToken);
            _logger.LogWarning("CTDIAG enqueued {TChange} tx={Tx}", typeof(TChange).Name, item.TransactionId);
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
