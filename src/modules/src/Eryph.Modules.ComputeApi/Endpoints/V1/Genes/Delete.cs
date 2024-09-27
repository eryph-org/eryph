using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Eryph.Messages.Genes.Commands;
using Eryph.Modules.AspNetCore.ApiProvider.Endpoints;
using Eryph.Modules.AspNetCore.ApiProvider.Handlers;
using Eryph.Modules.AspNetCore.ApiProvider.Model;
using Eryph.Modules.AspNetCore.ApiProvider;
using Eryph.Modules.AspNetCore.ApiProvider.Model.V1;
using Eryph.StateDb.Model;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace Eryph.Modules.ComputeApi.Endpoints.V1.Genes;

public class Delete(
    IEntityOperationRequestHandler<Gene> operationHandler,
    ISingleEntitySpecBuilder<SingleEntityRequest, Gene> specBuilder)
    : OperationRequestEndpoint<SingleEntityRequest, Gene>(operationHandler, specBuilder)
{
    protected override object CreateOperationMessage(Gene model, SingleEntityRequest request)
    {
        return new RemoveGeneCommand
        {
            Id = model.Id
        };
    }

    [Authorize(Policy = "compute:genes:write")]
    [HttpDelete("genes/{id}")]
    [SwaggerOperation(
        Summary = "Removes a gene",
        Description = "Removes a gene from the local gene pool",
        OperationId = "Genes_Delete",
        Tags = ["Genes"])
    ]
    public override async Task<ActionResult<Operation>> HandleAsync(
        [FromRoute] SingleEntityRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(request.Id, out _))
            return NotFound();

        return await base.HandleAsync(request, cancellationToken);
    }
}