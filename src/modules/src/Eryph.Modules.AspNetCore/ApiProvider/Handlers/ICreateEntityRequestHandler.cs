using System;
using System.Threading;
using System.Threading.Tasks;
using Eryph.Modules.AspNetCore.ApiProvider.Model;
using Eryph.Modules.AspNetCore.ApiProvider.Model.V1;
using Microsoft.AspNetCore.Mvc;

namespace Eryph.Modules.AspNetCore.ApiProvider.Handlers
{
    // ReSharper disable once UnusedTypeParameter
    public interface ICreateEntityRequestHandler<TEntity>
    {
        Task<ActionResult<ListResponse<Operation>>> HandleOperationRequest(
            Func<object> createOperationFunc,
            CancellationToken cancellationToken);
    }
}