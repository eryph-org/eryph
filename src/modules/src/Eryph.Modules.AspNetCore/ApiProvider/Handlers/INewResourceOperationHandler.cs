using System;
using System.Threading;
using System.Threading.Tasks;
using Eryph.Modules.AspNetCore.ApiProvider.Model;
using Eryph.Modules.AspNetCore.ApiProvider.Model.V1;
using Microsoft.AspNetCore.Mvc;

namespace Eryph.Modules.AspNetCore.ApiProvider.Handlers
{
    public interface INewResourceOperationHandler<TModel> where TModel : StateDb.Model.Resource
    {
        Task<ActionResult<ListResponse<Operation>>> HandleOperationRequest(
            Func<object> createOperationFunc,
            CancellationToken cancellationToken);
    }
}