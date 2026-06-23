using System;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using LanguageExt;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Eryph.ModuleCore.ChangeTracking;

/// <summary>
/// Base class for change interceptors that detect changes in the database and enqueue them for export
/// to the on-disk config mirror.
/// </summary>
/// <remarks>
/// We use both <see cref="CreatedSavepointAsync(DbTransaction, TransactionEventData, CancellationToken)"/>
/// and <see cref="TransactionCommittingAsync(DbTransaction, TransactionEventData, InterceptionResult, CancellationToken)"/>
/// to detect changes. EF Core will implicitly create a savepoint when
/// <c>SaveChangesAsync()</c> is called. We must detect changes when a savepoint
/// is created. Otherwise, we would miss deleted entities as EF Core seems
/// to remove them from the change tracker after the <c>SaveChangesAsync()</c>.
/// </remarks>
public abstract class ChangeInterceptorBase<TChange> : DbTransactionInterceptor
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
        await DetectAndStoreChanges(eventData, cancellationToken);
        await base.CreatedSavepointAsync(transaction, eventData, cancellationToken);
    }

    public override async ValueTask<InterceptionResult> TransactionCommittingAsync(
        DbTransaction transaction,
        TransactionEventData eventData,
        InterceptionResult result,
        CancellationToken cancellationToken = default)
    {
        await DetectAndStoreChanges(eventData, cancellationToken);
        return await base.TransactionCommittingAsync(transaction, eventData, result, cancellationToken);
    }

    public override async Task TransactionCommittedAsync(
        DbTransaction transaction,
        TransactionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        await EnqueueDetectedChanges(cancellationToken);
        await base.TransactionCommittedAsync(transaction, eventData, cancellationToken);
    }

    // OpenIddict's EF Core stores commit the delete path with a synchronous
    // RelationalTransaction.Commit() (inside an execution strategy), so EF Core
    // invokes these synchronous interceptor callbacks rather than the async ones.
    // They must mirror the async work: throwing from the pre-commit callbacks
    // (CreatedSavepoint/TransactionCommitting) aborts the commit and surfaces as a
    // 500 - the original bug - while skipping the post-commit enqueue would silently
    // lose the deletion from the on-disk config mirror.
    public override void CreatedSavepoint(
        DbTransaction transaction,
        TransactionEventData eventData)
    {
        DetectAndStoreChanges(eventData).GetAwaiter().GetResult();
        base.CreatedSavepoint(transaction, eventData);
    }

    public override InterceptionResult TransactionCommitting(
        DbTransaction transaction,
        TransactionEventData eventData,
        InterceptionResult result)
    {
        DetectAndStoreChanges(eventData).GetAwaiter().GetResult();
        return base.TransactionCommitting(transaction, eventData, result);
    }

    public override void TransactionCommitted(
        DbTransaction transaction,
        TransactionEndEventData eventData)
    {
        EnqueueDetectedChanges().GetAwaiter().GetResult();
        base.TransactionCommitted(transaction, eventData);
    }

    private async Task DetectAndStoreChanges(
        TransactionEventData eventData,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is null)
            return;

        var currentChanges = await DetectChanges(eventData.Context, cancellationToken)
            .MapT(changes => new ChangeTrackingQueueItem<TChange>(eventData.TransactionId, changes));

        _changes = _changes.Union(currentChanges);
    }

    private async Task EnqueueDetectedChanges(CancellationToken cancellationToken = default)
    {
        foreach (var item in _changes)
        {
            _logger.LogDebug("Detected relevant changes in transaction {TransactionId}: {Changes}",
                item.TransactionId, item.Changes);
            await _queue.EnqueueAsync(item, cancellationToken);
        }

        // Reset after a committed transaction so a DbContext that is reused for a
        // second transaction does not re-enqueue the already-exported changes.
        _changes = HashSet<ChangeTrackingQueueItem<TChange>>.Empty;
    }
}
