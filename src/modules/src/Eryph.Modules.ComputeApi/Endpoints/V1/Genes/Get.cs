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
using Gene = Eryph.StateDb.Model.Gene;

namespace Eryph.Modules.ComputeApi.Endpoints.V1.Genes;

public class Get(
    IGetRequestHandler<Gene, GeneWithUsage> requestHandler,
    ISingleEntitySpecBuilder<SingleEntityRequest, Gene> specBuilder)
    : GetEntityEndpoint<SingleEntityRequest, GeneWithUsage, Gene>(requestHandler, specBuilder)
{
    [Authorize(Policy = "compute:genes:read")]
    [HttpGet("genes/{id}")]
    [SwaggerOperation(
            Summary = "Get a gene",
            Description = "Get a gene",
            OperationId = "Genes_Get",
            Tags = ["Genes"]),
    ]
    [SwaggerResponse(
            StatusCodes.Status200OK,
            "Success",
            typeof(GeneWithUsage), "application/json"),
    ]
    public override async Task<ActionResult<GeneWithUsage>> HandleAsync(
        [FromRoute] SingleEntityRequest request,
        CancellationToken cancellationToken = default)
    {
        return await base.HandleAsync(request, cancellationToken);
    }
}
