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

namespace Eryph.Modules.ComputeApi.Endpoints.V1.Catlets;

public class List(
    [NotNull] IProjectListRequestHandler<ListEntitiesFilteredByProjectRequest, Catlet, StateDb.Model.Catlet> listRequestHandler,
    [NotNull] IListEntitySpecBuilder<ListEntitiesFilteredByProjectRequest, StateDb.Model.Catlet> specBuilder)
    : ListEntityEndpoint<ListEntitiesFilteredByProjectRequest, Catlet, StateDb.Model.Catlet>(listRequestHandler, specBuilder)
{
    [Authorize(Policy = "compute:catlets:read")]
    [HttpGet("catlets")]
    [SwaggerOperation(
        Summary = "List all catlets",
        Description = "List all catlets",
        OperationId = "Catlets_List",
        Tags = ["Catlets"])
    ]
    [SwaggerResponse(Microsoft.AspNetCore.Http.StatusCodes.Status200OK, "Success", typeof(ListEntitiesResponse<Catlet>))]
    public override Task<ActionResult<ListEntitiesResponse<Catlet>>> HandleAsync(
        [FromRoute] ListEntitiesFilteredByProjectRequest request,
        CancellationToken cancellationToken = default)
    {
        return base.HandleAsync(request, cancellationToken);
    }
}
