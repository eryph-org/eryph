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

        public async Task<TEntity> AddAsync(TEntity entity, CancellationToken cancellationToken = default)
        {
            var result = await _repository.AddAsync(entity, cancellationToken);
            await _repository.SaveChangesAsync(cancellationToken);
            return result;
        }

        public async Task UpdateAsync(TEntity entity, CancellationToken cancellationToken = default)
        {
            await _repository.UpdateAsync(entity, cancellationToken);
            await _repository.SaveChangesAsync(cancellationToken);
        }

        public async Task DeleteAsync(TEntity entity, CancellationToken cancellationToken = default)
        {
            await _repository.DeleteAsync(entity, cancellationToken);
            await _repository.SaveChangesAsync(cancellationToken);
        }
    }
}
