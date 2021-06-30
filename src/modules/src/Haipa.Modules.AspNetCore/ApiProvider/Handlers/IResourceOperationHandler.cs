using System;
using System.Threading;
using System.Threading.Tasks;
using Haipa.Data;
using Haipa.Modules.AspNetCore.ApiProvider.Model;
using Haipa.Modules.AspNetCore.ApiProvider.Model.V1;
using Microsoft.AspNetCore.Mvc;

namespace Haipa.Modules.AspNetCore.ApiProvider.Handlers
{
    public interface IResourceOperationHandler<TModel> where TModel : StateDb.Model.Resource
    {
        Task<ActionResult<ListResponse<Operation>>> HandleOperationRequest(
            Func<ISingleResultSpecification<TModel>> specificationFunc, 
            Func<TModel, object> createOperationFunc, 
            CancellationToken cancellationToken);
    }
}