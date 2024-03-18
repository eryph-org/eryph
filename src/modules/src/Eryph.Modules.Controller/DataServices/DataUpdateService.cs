using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Eryph.StateDb;

namespace Eryph.Modules.Controller.DataServices
{
    public class DataUpdateService<TEntity> : IDataUpdateService<TEntity>
        where TEntity : class
    {
        private readonly IStateStoreRepository<TEntity> _repository;

        public DataUpdateService(
            IStateStoreRepository<TEntity> repository)
        {
            _repository = repository;
        }

        public Task<TEntity> AddAsync(TEntity entity, CancellationToken cancellationToken = default)
        {
            return _repository.AddAsync(entity, cancellationToken);
        }

        public Task UpdateAsync(TEntity entity, CancellationToken cancellationToken = default)
        {
            return _repository.UpdateAsync(entity, cancellationToken);
        }

        public Task DeleteAsync(TEntity entity, CancellationToken cancellationToken = default)
        {
            return _repository.DeleteAsync(entity, cancellationToken);
        }
    }
}
