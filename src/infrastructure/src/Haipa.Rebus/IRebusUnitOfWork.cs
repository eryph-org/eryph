using System;
using System.Threading.Tasks;

namespace Haipa.Rebus
{
    public interface IRebusUnitOfWork : IAsyncDisposable, IDisposable
    {
        public Task Commit();
    }
}