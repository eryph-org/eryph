using System.Threading;
using System.Threading.Tasks;
using Ardalis.ApiEndpoints;
using Eryph.Modules.AspNetCore.ApiProvider.Handlers;
using Eryph.Modules.AspNetCore.ApiProvider.Model;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using Operation = Eryph.Modules.AspNetCore.ApiProvider.Model.V1.Operation;

namespace Eryph.Modules.AspNetCore.ApiProvider.Endpoints
{
    [Route("v{version:apiVersion}")]
    public abstract class NewOperationRequestEndpoint<TRequest, TModel> : EndpointBaseAsync
        .WithRequest<TRequest>
        .WithActionResult<ListResponse<Operation>>
        where TRequest : RequestBase

    {
        [NotNull] private readonly ICreateEntityRequestHandler<TModel> _operationHandler;

        protected NewOperationRequestEndpoint(
            [NotNull] ICreateEntityRequestHandler<TModel> operationHandler)
        {
            _operationHandler = operationHandler;
        }

        protected abstract object? CreateOperationMessage(TRequest request);


        [SwaggerResponse(Microsoft.AspNetCore.Http.StatusCodes.Status202Accepted, "Success", typeof(Operation))]
        public override Task<ActionResult<ListResponse<Operation>>> HandleAsync(TRequest request, CancellationToken cancellationToken = default)
        {
            return _operationHandler.HandleOperationRequest(() => CreateOperationMessage(request), cancellationToken);
        }
    }
}