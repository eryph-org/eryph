using System;
using System.Threading;
using System.Threading.Tasks;
using Eryph.Modules.AspNetCore.ApiProvider.Model.V1;
using Microsoft.AspNetCore.Mvc;

namespace Eryph.Modules.AspNetCore.ApiProvider.Handlers;

public interface IOperationRequestHandler<TEntity> where TEntity : class
{
    Task<ActionResult<Operation>> HandleOperationRequest(
        Func<object> createOperationFunc,
        CancellationToken cancellationToken);
}
