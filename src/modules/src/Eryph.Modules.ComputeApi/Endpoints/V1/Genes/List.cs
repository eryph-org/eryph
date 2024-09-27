using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Eryph.Modules.AspNetCore.ApiProvider.Endpoints;
using Eryph.Modules.AspNetCore.ApiProvider.Handlers;
using Eryph.Modules.AspNetCore.ApiProvider.Model;
using Eryph.Modules.AspNetCore.ApiProvider;
using Eryph.Modules.ComputeApi.Model.V1;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace Eryph.Modules.ComputeApi.Endpoints.V1.Genes;

public class List(
    [NotNull] IListRequestHandler<ListEntitiesRequest, Gene, StateDb.Model.Gene> listRequestHandler,
    [NotNull] IListEntitySpecBuilder<ListEntitiesRequest, StateDb.Model.Gene> specBuilder)
    : ListEntityEndpoint<ListEntitiesRequest, Gene, StateDb.Model.Gene>(listRequestHandler, specBuilder)
{
    [Authorize(Policy = "compute:genes:read")]
    [HttpGet("genes")]
    [SwaggerOperation(
        Summary = "List all genes",
        Description = "List all genes",
        OperationId = "Genes_List",
        Tags = ["Genes"])
    ]
    [SwaggerResponse(Microsoft.AspNetCore.Http.StatusCodes.Status200OK, "Success", typeof(ListEntitiesResponse<Gene>))]
    public override Task<ActionResult<ListEntitiesResponse<Gene>>> HandleAsync(
        [FromRoute] ListEntitiesRequest request,
        CancellationToken cancellationToken = default)
    {
        return base.HandleAsync(request, cancellationToken);
    }
}
