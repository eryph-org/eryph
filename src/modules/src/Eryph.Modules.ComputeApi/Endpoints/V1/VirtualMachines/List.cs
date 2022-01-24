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
