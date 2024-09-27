using System.Threading;
using System.Threading.Tasks;
using Eryph.Modules.AspNetCore.ApiProvider;
using Eryph.Modules.AspNetCore.ApiProvider.Endpoints;
using Eryph.Modules.AspNetCore.ApiProvider.Handlers;
using Eryph.Modules.AspNetCore.ApiProvider.Model;
using Eryph.Modules.ComputeApi.Model;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace Eryph.Modules.ComputeApi.Endpoints.V1.VirtualNetworks;

public class List(
    [NotNull] IProjectListRequestHandler<ProjectListRequest, VirtualNetwork, StateDb.Model.VirtualNetwork> listRequestHandler,
    [NotNull] IListEntitySpecBuilder<ProjectListRequest, StateDb.Model.VirtualNetwork> specBuilder)
    : ListEntityEndpoint<ProjectListRequest, VirtualNetwork, StateDb.Model.VirtualNetwork>(listRequestHandler, specBuilder)
{
    [Authorize(Policy = "compute:projects:read")]
    [HttpGet("vnetworks")]
    [SwaggerOperation(
        Summary = "Get list of virtual networks",
        Description = "Get list of virtual networks",
        OperationId = "VNetworks_List",
        Tags = ["Virtual Networks"])
    ]
    [SwaggerResponse(Microsoft.AspNetCore.Http.StatusCodes.Status200OK, "Success", typeof(ListResponse<VirtualNetwork>))]
    public override Task<ActionResult<ListResponse<VirtualNetwork>>> HandleAsync(
        [FromRoute] ProjectListRequest request,
        CancellationToken cancellationToken = default)
    {
        return base.HandleAsync(request, cancellationToken);
    }
}
