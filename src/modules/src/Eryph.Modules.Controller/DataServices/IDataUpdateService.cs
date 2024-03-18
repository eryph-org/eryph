using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Eryph.Modules.Controller.DataServices
{
    public interface IDataUpdateService<T> where T : class
    {
        public Task<T> AddAsync(T entity, CancellationToken cancellationToken = default);

        public Task UpdateAsync(T entity, CancellationToken cancellationToken = default);

        public Task DeleteAsync(T entity, CancellationToken cancellationToken = default);
    }
}
