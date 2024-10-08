using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Eryph.Modules.AspNetCore.ApiProvider.Endpoints;
using Eryph.Modules.AspNetCore.ApiProvider.Handlers;
using Eryph.Modules.AspNetCore.ApiProvider.Model;
using Eryph.Modules.AspNetCore.ApiProvider;
using Eryph.Modules.ComputeApi.Model.V1;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;

namespace Eryph.Modules.ComputeApi.Endpoints.V1.Genes;

public class Get(
    IGetRequestHandler<StateDb.Model.Gene, GeneWithUsage> requestHandler,
    ISingleEntitySpecBuilder<SingleEntityRequest, StateDb.Model.Gene> specBuilder)
    : GetEntityEndpoint<SingleEntityRequest, GeneWithUsage, StateDb.Model.Gene>(requestHandler, specBuilder)
{
    [Authorize(Policy = "compute:genes:read")]
    [HttpGet("genes/{id}")]
    [SwaggerOperation(
        Summary = "Get a gene",
        Description = "Get a gene",
        OperationId = "Genes_Get",
        Tags = ["Genes"])
    ]
    [SwaggerResponse(
        statusCode: StatusCodes.Status200OK,
        description: "Success",
        type: typeof(GeneWithUsage),
        contentTypes: ["application/json"])
    ]
    public override async Task<ActionResult<GeneWithUsage>> HandleAsync(
        [FromRoute] SingleEntityRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(request.Id, out _))
            return NotFound();

        return await base.HandleAsync(request, cancellationToken);
    }
}
