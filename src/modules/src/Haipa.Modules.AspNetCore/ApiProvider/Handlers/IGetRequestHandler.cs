using System;
using System.Threading;
using System.Threading.Tasks;
using Haipa.Data;
using Microsoft.AspNetCore.Mvc;

namespace Haipa.Modules.AspNetCore.ApiProvider.Handlers
{
    public interface IGetRequestHandler<TModel> where TModel : class
    {
        Task<ActionResult<TResponse>> HandleGetRequest<TResponse>(
            Func<ISingleResultSpecification<TModel>> specificationFunc,
            CancellationToken cancellationToken);

    }
}