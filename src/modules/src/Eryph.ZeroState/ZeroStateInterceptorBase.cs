using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LanguageExt;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Eryph.ZeroState
{
    public interface IZeroStateInterceptor : IInterceptor
    {
    }

    public abstract class ZeroStateInterceptorBase<TChange>
        : DbTransactionInterceptor, IZeroStateInterceptor
    {
        private readonly IZeroStateQueue<TChange> _queue;
        private Option<ZeroStateQueueItem2<TChange>> _currentItem = Option<ZeroStateQueueItem2<TChange>>.None;

        protected ZeroStateInterceptorBase(
            IZeroStateQueue<TChange> queue)
        {
            _queue = queue;
        }

        protected abstract Task<Option<TChange>> DetectChanges(
            TransactionEventData eventData,
            CancellationToken cancellationToken = default);

        public override async ValueTask<InterceptionResult> TransactionCommittingAsync(
            DbTransaction transaction,
            TransactionEventData eventData,
            InterceptionResult result,
            CancellationToken cancellationToken = default)
        {
            _currentItem = await DetectChanges(eventData, cancellationToken)
                .MapT(changes => new ZeroStateQueueItem2<TChange>()
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
}
