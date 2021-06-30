using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Ardalis.Specification;
using Haipa.Modules.AspNetCore.ApiProvider;
using Haipa.Modules.AspNetCore.ApiProvider.Endpoints;
using Haipa.Modules.AspNetCore.ApiProvider.Handlers;
using Haipa.Modules.AspNetCore.ApiProvider.Model;
using Haipa.Modules.ComputeApi.Model.V1;
using Haipa.StateDb;
using Haipa.StateDb.Specifications;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace Haipa.Modules.ComputeApi.Endpoints.V1.Machines
{
    public class List : ListResourceEndpoint<ListRequest,Machine, StateDb.Model.Machine>
    {
        public List([NotNull] IListRequestHandler<StateDb.Model.Machine> listRequestHandler, [NotNull] IListResourceSpecBuilder<StateDb.Model.Machine> specBuilder) : base(listRequestHandler, specBuilder)
        {
        }

        protected override ISpecification<StateDb.Model.Machine> CreateSpecification(ListRequest request)
        {
            return new ResourceSpecs<StateDb.Model.Machine>.GetAll();
        }

        [HttpGet("machines")]
        [SwaggerOperation(
            Summary = "List all Machines",
            Description = "List all Machines",
            OperationId = "Machines_List",
            Tags = new[] { "Machines" })
        ]
        [SwaggerResponse(Microsoft.AspNetCore.Http.StatusCodes.Status200OK, "Success", typeof(ListResponse<Machine>))]
        public override Task<ActionResult<ListResponse<Machine>>> HandleAsync([FromRoute] ListRequest request, CancellationToken cancellationToken = default)
        {
            return base.HandleAsync(request, cancellationToken);
        }


    }
}
