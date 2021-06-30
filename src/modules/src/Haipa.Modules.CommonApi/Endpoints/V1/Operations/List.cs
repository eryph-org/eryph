using System;
using System.Collections.Generic;
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

namespace Haipa.Modules.ComputeApi.Endpoints.V1.Machines
{
    public class List : ListEndpoint<OperationsListRequest, Operation, StateDb.Model.Operation>
    {
        public List([NotNull] IListRequestHandler<StateDb.Model.Operation> listRequestHandler) : base(listRequestHandler)
        {
        }

        protected override ISpecification<StateDb.Model.Operation> CreateSpecification(OperationsListRequest request)
        {
            return new OperationSpecs.GetAll(true, request.LogTimestamp);
        }

        [HttpGet("operations")]
        [SwaggerOperation(
            Summary = "List all Operations",
            Description = "List all Operations",
            OperationId = "Operations_List",
            Tags = new[] { "Operations" })
        ]
        [SwaggerResponse(Microsoft.AspNetCore.Http.StatusCodes.Status200OK, "Success", typeof(ListResponse<Operation>))]
        public override Task<ActionResult<ListResponse<Operation>>> HandleAsync([FromRoute] OperationsListRequest request, CancellationToken cancellationToken = default)
        {
            return base.HandleAsync(request, cancellationToken);
        }


    }

    public class OperationsListRequest : ListRequest
    {
        /// <summary>
        /// Filters returned log entries by the requested timestamp
        /// </summary>
        [FromQuery(Name = "logTimeStamp")] public DateTimeOffset LogTimestamp { get; set; }
    }
}
