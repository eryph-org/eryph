using System.Threading.Tasks;
using Rebus.Config;
using Rebus.Pipeline;

namespace Haipa.Rebus
{
    public static class SimpleInjectorUnitOfWork
    {
        public static void EnableSimpleInjectorUnitOfWork(
            this OptionsConfigurer configurer)
        {
            configurer.EnableAsyncUnitOfWork(Create, Commit, dispose: Dispose);
        }

        private static Task<RebusUnitOfWorkAdapter> Create(IMessageContext context)
        {
            var unitOfWork = new RebusUnitOfWorkAdapter();
            // stash current unit of work in the transaction context's items
            context.TransactionContext.Items["uow"] = unitOfWork;

            return Task.FromResult(unitOfWork);
        }

        private static Task Commit(IMessageContext context, RebusUnitOfWorkAdapter uow)
        {
            return uow.Commit(context);
        }

        private static Task Dispose(IMessageContext context, RebusUnitOfWorkAdapter uow)
        {
            return uow.Dispose(context);
        }
    }
}