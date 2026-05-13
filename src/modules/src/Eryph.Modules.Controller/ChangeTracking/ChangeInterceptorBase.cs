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
/// Changes are captured from multiple hooks so that we observe the change
/// tracker while it is still fully populated regardless of provider/version
/// quirks: <see cref="ISaveChangesInterceptor.SavingChangesAsync"/> is the
/// primary capture point (always fires before EF clears deleted entries),
/// while <see cref="CreatedSavepointAsync"/> and
/// <see cref="TransactionCommittingAsync"/> remain as defensive fallbacks.
/// Enqueueing is deferred until <see cref="TransactionCommittedAsync"/> so
/// downstream handlers only see changes that were actually committed.
/// </remarks>
internal abstract class ChangeInterceptorBase<TChange> : DbTransactionInterceptor, ISaveChangesInterceptor
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

    public async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is null)
            return result;

        var transactionId = eventData.Context.Database.CurrentTransaction?.TransactionId ?? Guid.Empty;
        await CaptureChangesAsync(eventData.Context, transactionId, cancellationToken);
        return result;
    }

    public override async Task CreatedSavepointAsync(
        DbTransaction transaction,
        TransactionEventData eventData,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null)
            await CaptureChangesAsync(eventData.Context, eventData.TransactionId, cancellationToken);

        await base.CreatedSavepointAsync(transaction, eventData, cancellationToken);
    }

    public override async ValueTask<InterceptionResult> TransactionCommittingAsync(
        DbTransaction transaction,
        TransactionEventData eventData,
        InterceptionResult result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null)
            await CaptureChangesAsync(eventData.Context, eventData.TransactionId, cancellationToken);

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

        _changes = HashSet<ChangeTrackingQueueItem<TChange>>.Empty;
    }

    private async Task CaptureChangesAsync(
        DbContext context,
        Guid transactionId,
        CancellationToken cancellationToken)
    {
        var currentChanges = await DetectChanges(context, cancellationToken)
            .MapT(changes => new ChangeTrackingQueueItem<TChange>(transactionId, changes));

        _changes = _changes.Union(currentChanges);
    }

    int ISaveChangesInterceptor.SavedChanges(SaveChangesCompletedEventData eventData, int result) => result;

    InterceptionResult<int> ISaveChangesInterceptor.SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result) => throw new NotSupportedException();

    void ISaveChangesInterceptor.SaveChangesFailed(DbContextErrorEventData eventData) { }

    Task ISaveChangesInterceptor.SaveChangesFailedAsync(
        DbContextErrorEventData eventData,
        CancellationToken cancellationToken) => Task.CompletedTask;

    ValueTask<int> ISaveChangesInterceptor.SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken) => ValueTask.FromResult(result);

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
