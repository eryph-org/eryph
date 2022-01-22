using System;
using System.Threading;
using System.Threading.Tasks;
using Ardalis.ApiEndpoints;
using Ardalis.Specification;
using Haipa.Modules.AspNetCore.ApiProvider.Handlers;
using Haipa.Modules.AspNetCore.ApiProvider.Model;
using Haipa.StateDb.Model;
using Haipa.StateDb.Specifications;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using Operation = Haipa.Modules.AspNetCore.ApiProvider.Model.V1.Operation;

namespace Haipa.Modules.AspNetCore.ApiProvider.Endpoints
{
    [Route("v{version:apiVersion}")]
    public abstract class ResourceOperationEndpoint<TRequest, TModel> : EndpointBaseAsync
        .WithRequest<TRequest>
        .WithActionResult<ListResponse<Operation>> where TModel : Resource
        where TRequest: SingleResourceRequest

    {
        [NotNull] private readonly IResourceOperationHandler<TModel> _operationHandler;

        protected ResourceOperationEndpoint(
            [NotNull] IResourceOperationHandler<TModel> operationHandler)
        {
            _operationHandler = operationHandler;
        }

        protected abstract object CreateOperationMessage(TModel model, TRequest request);

        private ISingleResultSpecification<TModel> CreateSpecification(SingleResourceRequest request)
        {
            return new ResourceSpecs<TModel>.GetById(Guid.Parse(request.Id ?? throw new InvalidOperationException("Invalid id")));
        }


        [SwaggerResponse(Microsoft.AspNetCore.Http.StatusCodes.Status202Accepted, "Success", typeof(Operation))]

        public override Task<ActionResult<ListResponse<Operation>>> HandleAsync(TRequest request, CancellationToken cancellationToken = default)
        {
            return _operationHandler.HandleOperationRequest(() => CreateSpecification(request), m => CreateOperationMessage(m, request), cancellationToken);
        }
    }
}