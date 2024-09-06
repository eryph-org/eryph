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
using JetBrains.Annotations;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using Microsoft.AspNetCore.Authorization;

namespace Eryph.Modules.ComputeApi.Endpoints.V1.Genes;

public class Get(
    [NotNull] IGetRequestHandler<StateDb.Model.Gene, GeneWithUsage> requestHandler,
    [NotNull] ISingleEntitySpecBuilder<SingleEntityRequest, StateDb.Model.Gene> specBuilder)
    : GetEntityEndpoint<SingleEntityRequest, GeneWithUsage, StateDb.Model.Gene>(requestHandler, specBuilder)
{
    [Authorize(Policy = "compute:genes:read")]
    [HttpGet("genes/{id}")]
    [SwaggerOperation(
        Summary = "Gene a gene",
        Description = "Get a gene",
        OperationId = "Genes_Get",
        Tags = ["Genes"])
    ]
    [SwaggerResponse(Microsoft.AspNetCore.Http.StatusCodes.Status200OK, "Success", typeof(GeneWithUsage))]
    public override async Task<ActionResult<GeneWithUsage>> HandleAsync(
        [FromRoute] SingleEntityRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(request.Id, out _))
            return NotFound();

        return await base.HandleAsync(request, cancellationToken);
    }
}
