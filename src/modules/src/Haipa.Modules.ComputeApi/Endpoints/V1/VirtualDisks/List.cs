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

namespace Haipa.Modules.ComputeApi.Endpoints.V1.VirtualDisks
{
    public class List : ListResourceEndpoint<ListRequest, VirtualDisk, StateDb.Model.VirtualDisk>
    {
        public List([NotNull] IListRequestHandler<StateDb.Model.VirtualDisk> listRequestHandler, [NotNull] IListResourceSpecBuilder<StateDb.Model.VirtualDisk> specBuilder) : base(listRequestHandler, specBuilder)
        {
        }

        [HttpGet("virtualdisks")]
        [SwaggerOperation(
            Summary = "Get list of Virtual Disks",
            Description = "Get list of Virtual Disks",
            OperationId = "VirtualDisks_List",
            Tags = new[] { "Virtual Disks" })
        ]
        [SwaggerResponse(Microsoft.AspNetCore.Http.StatusCodes.Status200OK, "Success", typeof(ListResponse<VirtualDisk>))]
        public override Task<ActionResult<ListResponse<VirtualDisk>>> HandleAsync([FromRoute] ListRequest request, CancellationToken cancellationToken = default)
        {
            return base.HandleAsync(request, cancellationToken);
        }

    }
}
