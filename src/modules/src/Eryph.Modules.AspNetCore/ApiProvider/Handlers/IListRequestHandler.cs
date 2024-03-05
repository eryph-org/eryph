using System;
using System.Threading;
using System.Threading.Tasks;
using Ardalis.Specification;
using Eryph.Modules.AspNetCore.ApiProvider.Model;
using Microsoft.AspNetCore.Mvc;

namespace Eryph.Modules.AspNetCore.ApiProvider.Handlers
{

    public interface IListRequestHandler<TModel> where TModel : class
    {
        Task<ActionResult<ListResponse<TResponse>>> HandleListRequest<TRequest,TResponse>(
            TRequest request,
            Func<TRequest, ISpecification<TModel>> createSpecificationFunc, CancellationToken cancellationToken)
            where TRequest: IListRequest;
        
    }
}