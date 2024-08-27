using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Eryph.Messages.Resources.Genes.Commands;
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
    [NotNull] IOperationRequestHandler<Gene> operationHandler,
    [NotNull] ISingleEntitySpecBuilder<SingleEntityRequest, Gene> specBuilder)
    : OperationRequestEndpoint<SingleEntityRequest, Gene>(operationHandler, specBuilder)
{
    protected override object CreateOperationMessage(Gene model, SingleEntityRequest request)
    {
        return new RemoveGeneCommand
        {
            Id = model.Id
        };
    }

    [Authorize(Policy = "compute:catlets:write")]
    [HttpDelete("genes/{id}")]
    [SwaggerOperation(
        Summary = "Deletes a gene",
        Description = "Deletes a gene",
        OperationId = "Genes_Delete",
        Tags = ["Genes"])
    ]
    public override async Task<ActionResult<ListResponse<Operation>>> HandleAsync(
        [FromRoute] SingleEntityRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(request.Id, out _))
            return NotFound();

        return await base.HandleAsync(request, cancellationToken);
    }
}