using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LanguageExt;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Eryph.Modules.Controller.ChangeTracking;

internal class ChangeTrackingSaveChangesInterceptor<TChange>(
    IChangeDetector<TChange> detector,
    IChangeTrackingQueue<TChange> queue,
    ILogger logger)
    : SaveChangesInterceptor
{
    private Seq<ChangeTrackingQueueItem<TChange>> _currentItem = Prelude.Empty;

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        // EF Core 7 and later does not create database transactions when saving
        // changes unless they are required. When a transaction is in progress, the
        // changes will be handled by the transaction interceptor. Otherwise, we
        // need to detect the changes here.
        if (eventData.Context is null || eventData.Context.Database.CurrentTransaction is not null)
            return await base.SavingChangesAsync(eventData, result, cancellationToken);
        
        _currentItem = await detector.DetectChanges(eventData.Context, cancellationToken)
            .MapT(changes => new ChangeTrackingQueueItem<TChange>(null, changes));

        return await base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        foreach (var item in _currentItem)
        {
            logger.LogDebug("Detected relevant changes when saving to database: {Changes}",
                item.Changes);
            await queue.EnqueueAsync(item, cancellationToken);
        }

        return await base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        throw new NotSupportedException();
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        throw new NotSupportedException();
    }
}
