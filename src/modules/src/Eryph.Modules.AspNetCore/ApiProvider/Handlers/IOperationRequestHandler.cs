using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Eryph.Modules.AspNetCore.ApiProvider.Model;
using Eryph.Modules.AspNetCore.ApiProvider.Model.V1;
using Microsoft.AspNetCore.Mvc;

namespace Eryph.Modules.AspNetCore.ApiProvider.Handlers;

public interface IOperationRequestHandler<TEntity> where TEntity : class
{
    Task<ActionResult<ListResponse<Operation>>> HandleOperationRequest(
        Func<object> createOperationFunc,
        CancellationToken cancellationToken);
}
