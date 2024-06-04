using System.Threading;
using System.Threading.Tasks;
using Eryph.Modules.AspNetCore.ApiProvider;
using Eryph.Modules.AspNetCore.ApiProvider.Endpoints;
using Eryph.Modules.AspNetCore.ApiProvider.Handlers;
using Eryph.Modules.AspNetCore.ApiProvider.Model;
using Eryph.Modules.AspNetCore.ApiProvider.Model.V1;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace Eryph.Modules.ComputeApi.Endpoints.V1.Operations;

public class List(
    [NotNull]
    IListRequestHandler<OperationsListRequest, Operation, StateDb.Model.OperationModel> listRequestHandler,
    [NotNull] IListEntitySpecBuilder<OperationsListRequest, StateDb.Model.OperationModel> specBuilder)
    : ListEntityEndpoint<OperationsListRequest, Operation, StateDb.Model.OperationModel>(
        listRequestHandler, specBuilder)
{
    [HttpGet("operations")]
    [SwaggerOperation(
        Summary = "List all Operations",
        Description = "List all Operations",
        OperationId = "Operations_List",
        Tags = ["Operations"])
    ]
    [SwaggerResponse(Microsoft.AspNetCore.Http.StatusCodes.Status200OK, "Success", typeof(ListResponse<Operation>))]
    public override Task<ActionResult<ListResponse<Operation>>> HandleAsync(
        [FromRoute] OperationsListRequest request,
        CancellationToken cancellationToken = default)
    {
        return base.HandleAsync(request, cancellationToken);
    }
}
