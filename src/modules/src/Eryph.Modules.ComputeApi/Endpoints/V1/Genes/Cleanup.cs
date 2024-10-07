using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Eryph.Messages.Genes.Commands;
using Eryph.Modules.AspNetCore.ApiProvider.Endpoints;
using Eryph.Modules.AspNetCore.ApiProvider.Handlers;
using Eryph.Modules.AspNetCore.ApiProvider.Model.V1;
using Eryph.StateDb.Model;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace Eryph.Modules.ComputeApi.Endpoints.V1.Genes;

public class Cleanup(IOperationRequestHandler<Gene> operationHandler)
    : OperationRequestEndpoint<Gene>(operationHandler)
{
    protected override object CreateOperationMessage()
    {
        return new CleanupGenesCommand();
    }

    [Authorize(Policy = "compute:genes:write")]
    [HttpDelete("genes")]
    [SwaggerOperation(
        Summary = "Removes unused genes",
        Description = "Removes unused genes from the local gene pool",
        OperationId = "Genes_Cleanup",
        Tags = ["Genes"])
    ]
    public override Task<ActionResult<Operation>> HandleAsync(
        CancellationToken cancellationToken = default)
    {
        return base.HandleAsync(cancellationToken);
    }
}
