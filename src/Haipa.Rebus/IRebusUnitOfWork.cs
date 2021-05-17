using System;
using System.Threading.Tasks;
using SimpleInjector;

namespace Haipa.Rebus
{
    public interface IRebusUnitOfWork : IAsyncDisposable
    {
        public Task Commit();

    }
}