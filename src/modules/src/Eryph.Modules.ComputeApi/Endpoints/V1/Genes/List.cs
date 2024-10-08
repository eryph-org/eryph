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
        Tags = ["Genes"])
    ]
    [SwaggerResponse(
        statusCode: StatusCodes.Status200OK,
        description: "Success",
        type: typeof(ListResponse<Gene>),
        contentTypes: ["application/json"])
    ]
    public override Task<ActionResult<ListResponse<Gene>>> HandleAsync(
        CancellationToken cancellationToken = default)
    {
        return base.HandleAsync(cancellationToken);
    }
}
