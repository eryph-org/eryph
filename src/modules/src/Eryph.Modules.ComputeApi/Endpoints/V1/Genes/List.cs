using System.Threading;
using System.Threading.Tasks;
using Eryph.Modules.AspNetCore.ApiProvider;
using Eryph.Modules.AspNetCore.ApiProvider.Endpoints;
using Eryph.Modules.AspNetCore.ApiProvider.Handlers;
using Eryph.Modules.AspNetCore.ApiProvider.Model;
using Eryph.Modules.ComputeApi.Model.V1;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace Eryph.Modules.ComputeApi.Endpoints.V1.Genes;

public class List(
    IListRequestHandler<Gene, StateDb.Model.Gene> listRequestHandler,
    IListEntitySpecBuilder<StateDb.Model.Gene> specBuilder)
    : ListEntitiesEndpoint<Gene, StateDb.Model.Gene>(listRequestHandler, specBuilder)
{
    [Authorize(Policy = "compute:genes:read")]
    [HttpGet("genes")]
    [SwaggerOperation(
            Summary = "List all genes",
            Description = "List all genes",
            OperationId = "Genes_List",
            Tags = ["Genes"]),
    ]
    [SwaggerResponse(
            StatusCodes.Status200OK,
            "Success",
            typeof(ListResponse<Gene>), "application/json"),
    ]
    public override Task<ActionResult<ListResponse<Gene>>> HandleAsync(
        CancellationToken cancellationToken = default)
    {
        return base.HandleAsync(cancellationToken);
    }
}
