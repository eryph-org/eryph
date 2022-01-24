using System;
using System.Threading.Tasks;

namespace Eryph.Rebus
{
    public interface IRebusUnitOfWork : IAsyncDisposable, IDisposable
    {
        public Task Commit();
    }
}