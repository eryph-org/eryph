using System;
using System.Threading;
using System.Threading.Tasks;
using Haipa.Modules.AspNetCore.ApiProvider.Model;
using Haipa.Modules.AspNetCore.ApiProvider.Model.V1;
using Microsoft.AspNetCore.Mvc;

namespace Haipa.Modules.AspNetCore.ApiProvider.Handlers
{
    public interface INewResourceOperationHandler<TModel> where TModel : StateDb.Model.Resource
    {
        Task<ActionResult<ListResponse<Operation>>> HandleOperationRequest(
            Func<object> createOperationFunc,
            CancellationToken cancellationToken);
    }
}