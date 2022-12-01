using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Ardalis.Specification;
using Eryph.Modules.AspNetCore.ApiProvider;
using Eryph.Modules.AspNetCore.ApiProvider.Endpoints;
using Eryph.Modules.AspNetCore.ApiProvider.Handlers;
using Eryph.Modules.AspNetCore.ApiProvider.Model;
using Eryph.Modules.ComputeApi.Model.V1;
using Eryph.StateDb.Specifications;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace Eryph.Modules.ComputeApi.Endpoints.V1.VirtualMachines
{
    public class List : ListResourceEndpoint<ListRequest, VirtualCatlet, StateDb.Model.VirtualCatlet>
    {
        public List([NotNull] IListRequestHandler<StateDb.Model.VirtualCatlet> listRequestHandler, [NotNull] IListResourceSpecBuilder<StateDb.Model.VirtualCatlet> specBuilder) : base(listRequestHandler, specBuilder)
        {

        }

        [HttpGet("virtualmachines")]
        [SwaggerOperation(
            Summary = "Get list of Virtual Machines",
            Description = "Get list of Virtual Machines",
            OperationId = "VirtualMachines_List",
            Tags = new[] { "Virtual Machines" })
        ]
        [SwaggerResponse(Microsoft.AspNetCore.Http.StatusCodes.Status200OK, "Success", typeof(ListResponse<VirtualCatlet>))]
        public override Task<ActionResult<ListResponse<VirtualCatlet>>> HandleAsync([FromRoute] ListRequest request, CancellationToken cancellationToken = default)
        {
            return base.HandleAsync(request, cancellationToken);
        }


    }
}
