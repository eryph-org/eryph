using System;
using System.Threading;
using System.Threading.Tasks;
using Ardalis.Specification;
using Eryph.Modules.AspNetCore.ApiProvider.Endpoints;
using Eryph.Modules.AspNetCore.ApiProvider.Handlers;
using Eryph.Modules.AspNetCore.ApiProvider.Model;
using Eryph.Modules.AspNetCore.ApiProvider.Model.V1;
using Eryph.StateDb.Specifications;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace Eryph.Modules.CommonApi.Endpoints.V1.Operations
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
