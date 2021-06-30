using System.Threading;
using System.Threading.Tasks;
using Ardalis.ApiEndpoints;
using Haipa.Modules.AspNetCore.ApiProvider.Handlers;
using Haipa.Modules.AspNetCore.ApiProvider.Model;
using Haipa.StateDb.Model;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using Operation = Haipa.Modules.AspNetCore.ApiProvider.Model.V1.Operation;

namespace Haipa.Modules.AspNetCore.ApiProvider.Endpoints
{
    [Route("v{version:apiVersion}")]
    public abstract class NewResourceOperationEndpoint<TRequest, TModel> : BaseAsyncEndpoint
        .WithRequest<TRequest>
        .WithResponse<ListResponse<Operation>> where TModel : Resource
        where TRequest : RequestBase

    {
        [NotNull] private readonly INewResourceOperationHandler<TModel> _operationHandler;

        protected NewResourceOperationEndpoint(
            [NotNull] INewResourceOperationHandler<TModel> operationHandler)
        {
            _operationHandler = operationHandler;
        }

        protected abstract object CreateOperationMessage(TRequest request);


        [SwaggerResponse(Microsoft.AspNetCore.Http.StatusCodes.Status202Accepted, "Success", typeof(Operation))]
        public override Task<ActionResult<ListResponse<Operation>>> HandleAsync(TRequest request, CancellationToken cancellationToken = default)
        {
            return _operationHandler.HandleOperationRequest(() => CreateOperationMessage(request), cancellationToken);
        }
    }
}