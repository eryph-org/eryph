using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Eryph.Modules.Controller.DataServices;

namespace Eryph.Runtime.Zero.Configuration
{
    internal class DecoratedDataUpdateService<TEntity> : IDataUpdateService<TEntity>
        where TEntity : class
    {
        private readonly IDataUpdateService<TEntity> _decoratedService;
        private readonly IConfigWriter<TEntity> _configWriter;

        public DecoratedDataUpdateService(
            IDataUpdateService<TEntity> decoratedService,
            IConfigWriter<TEntity> configWriter)
        {
            _decoratedService = decoratedService;
            _configWriter = configWriter;
        }

        public async Task<TEntity> AddAsync(TEntity entity, CancellationToken cancellationToken = default)
        {
            var result = await _decoratedService.AddAsync(entity, cancellationToken);
            await _configWriter.AddAsync(result, cancellationToken);

            return result;
        }

        public async Task UpdateAsync(TEntity entity, CancellationToken cancellationToken = default)
        {
            await _decoratedService.UpdateAsync(entity, cancellationToken);
            await _configWriter.UpdateAsync(entity, cancellationToken);
        }

        public async Task DeleteAsync(TEntity entity, CancellationToken cancellationToken = default)
        {
            await _decoratedService.DeleteAsync(entity, cancellationToken);
            await _configWriter.DeleteAsync(entity, cancellationToken);
        }
    }
}
