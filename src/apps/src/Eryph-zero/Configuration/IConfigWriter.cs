using Eryph.Modules.Controller.DataServices;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Eryph.Runtime.Zero.Configuration;

internal interface IConfigWriter<TEntity> where TEntity : class
{
    public Task AddAsync(TEntity entity, CancellationToken cancellationToken = default);

    public Task UpdateAsync(TEntity entity, CancellationToken cancellationToken = default);

    public Task DeleteAsync(TEntity entity, CancellationToken cancellationToken = default);
}
