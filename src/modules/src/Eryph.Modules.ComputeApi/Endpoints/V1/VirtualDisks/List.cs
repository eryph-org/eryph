using System.Threading;
using System.Threading.Tasks;
using Eryph.Modules.AspNetCore.ApiProvider;
using Eryph.Modules.AspNetCore.ApiProvider.Endpoints;
using Eryph.Modules.AspNetCore.ApiProvider.Handlers;
using Eryph.Modules.AspNetCore.ApiProvider.Model;
using Eryph.Modules.ComputeApi.Model.V1;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace Eryph.Modules.ComputeApi.Endpoints.V1.VirtualDisks;

public class List(
    [NotNull] IListRequestHandler<ListRequest, VirtualDisk, StateDb.Model.VirtualDisk> listRequestHandler,
    [NotNull] IListEntitySpecBuilder<ListRequest, StateDb.Model.VirtualDisk> specBuilder)
    : ListEntityEndpoint<ListRequest, VirtualDisk, StateDb.Model.VirtualDisk>(listRequestHandler, specBuilder)
{
    [Authorize(Policy = "compute:catlets:read")]
    [HttpGet("virtualdisks")]
    [SwaggerOperation(
        Summary = "Get list of Virtual Disks",
        Description = "Get list of Virtual Disks",
        OperationId = "VirtualDisks_List",
        Tags = ["Virtual Disks"])
    ]
    [SwaggerResponse(Microsoft.AspNetCore.Http.StatusCodes.Status200OK, "Success", typeof(ListResponse<VirtualDisk>))]
    public override Task<ActionResult<ListResponse<VirtualDisk>>> HandleAsync(
        [FromRoute] ListRequest request,
        CancellationToken cancellationToken = default)
    {
        return base.HandleAsync(request, cancellationToken);
    }
}
