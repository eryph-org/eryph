using System.Threading;
using System.Threading.Tasks;
using Ardalis.ApiEndpoints;
using Ardalis.Specification;
using Eryph.Modules.AspNetCore.ApiProvider.Handlers;
using Eryph.Modules.AspNetCore.ApiProvider.Model;
using Eryph.StateDb.Model;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using Operation = Eryph.Modules.AspNetCore.ApiProvider.Model.V1.Operation;

namespace Eryph.Modules.AspNetCore.ApiProvider.Endpoints
{
    [Route("v{version:apiVersion}")]
    public abstract class OperationRequestEndpoint<TRequest, TEntity> : EndpointBaseAsync
        .WithRequest<TRequest>
        .WithActionResult<ListResponse<Operation>> where TEntity : class
        where TRequest: SingleEntityRequest

    {
        [NotNull] private readonly IOperationRequestHandler<TEntity> _operationHandler;
        private readonly ISingleEntitySpecBuilder<TRequest, TEntity> _specBuilder;

        protected OperationRequestEndpoint(
            [NotNull] IOperationRequestHandler<TEntity> operationHandler, ISingleEntitySpecBuilder<TRequest,TEntity> specBuilder)
        {
            _operationHandler = operationHandler;
            _specBuilder = specBuilder;
        }

        protected abstract object CreateOperationMessage(TEntity model, TRequest request);

        private ISingleResultSpecification<TEntity>? CreateSpecification(TRequest request)
        {
            return _specBuilder.GetSingleEntitySpec(request, AccessRight.Write);
        }


        [SwaggerResponse(Microsoft.AspNetCore.Http.StatusCodes.Status202Accepted, "Success", typeof(Operation))]

        public override Task<ActionResult<ListResponse<Operation>>> HandleAsync(TRequest request, CancellationToken cancellationToken = default)
        {
            return _operationHandler.HandleOperationRequest(() => CreateSpecification(request), m => CreateOperationMessage(m, request), cancellationToken);
        }
    }
}