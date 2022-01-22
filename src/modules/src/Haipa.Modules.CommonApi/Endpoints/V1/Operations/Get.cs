using System;
using System.Threading;
using System.Threading.Tasks;
using Ardalis.Specification;
using Haipa.Modules.AspNetCore.ApiProvider;
using Haipa.Modules.AspNetCore.ApiProvider.Endpoints;
using Haipa.Modules.AspNetCore.ApiProvider.Handlers;
using Haipa.Modules.AspNetCore.ApiProvider.Model;
using Haipa.Modules.AspNetCore.ApiProvider.Model.V1;
using Haipa.StateDb.Specifications;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace Haipa.Modules.CommonApi.Endpoints.V1.Operations
{
    public class Get : SingleResultEndpoint<OperationRequest, Operation, StateDb.Model.Operation>
    {
        public Get([NotNull]IGetRequestHandler<StateDb.Model.Operation> requestHandler) : base(requestHandler)
        {
        }


        protected override ISingleResultSpecification<StateDb.Model.Operation> CreateSpecification(OperationRequest request)
        {
            return new OperationSpecs.GetById(Guid.Parse(request.Id), true, request.LogTimestamp);
        }

        [HttpGet("operations/{id}")]
        [SwaggerOperation(
            Summary = "Get a operation",
            Description = "Get a operation",
            OperationId = "Operations_Get",
            Tags = new[] { "Operations" })
        ]
        public override Task<ActionResult<Operation>> HandleAsync([FromRoute] OperationRequest request, CancellationToken cancellationToken = default)
        {
            return base.HandleAsync(request, cancellationToken);
        }

    }

    public class OperationRequest : SingleResourceRequest
    {
        /// <summary>
        /// Filters returned log entries by the requested timestamp
        /// </summary>
        [FromQuery(Name = "logTimeStamp")] public DateTimeOffset LogTimestamp { get; set; }

    }
}
