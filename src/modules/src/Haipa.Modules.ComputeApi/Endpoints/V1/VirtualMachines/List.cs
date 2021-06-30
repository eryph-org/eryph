using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Ardalis.Specification;
using Haipa.Modules.AspNetCore.ApiProvider;
using Haipa.Modules.AspNetCore.ApiProvider.Endpoints;
using Haipa.Modules.AspNetCore.ApiProvider.Handlers;
using Haipa.Modules.AspNetCore.ApiProvider.Model;
using Haipa.Modules.ComputeApi.Model.V1;
using Haipa.StateDb.Specifications;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace Haipa.Modules.ComputeApi.Endpoints.V1.VirtualMachines
{
    public class List : ListResourceEndpoint<ListRequest, VirtualMachine, StateDb.Model.VirtualMachine>
    {
        public List([NotNull] IListRequestHandler<StateDb.Model.VirtualMachine> listRequestHandler, [NotNull] IListResourceSpecBuilder<StateDb.Model.VirtualMachine> specBuilder) : base(listRequestHandler, specBuilder)
        {

        }

        [HttpGet("virtualmachines")]
        [SwaggerOperation(
            Summary = "Get list of Virtual Machines",
            Description = "Get list of Virtual Machines",
            OperationId = "VirtualMachines_List",
            Tags = new[] { "Virtual Machines" })
        ]
        [SwaggerResponse(Microsoft.AspNetCore.Http.StatusCodes.Status200OK, "Success", typeof(ListResponse<VirtualMachine>))]
        public override Task<ActionResult<ListResponse<VirtualMachine>>> HandleAsync([FromRoute] ListRequest request, CancellationToken cancellationToken = default)
        {
            return base.HandleAsync(request, cancellationToken);
        }


    }
}
