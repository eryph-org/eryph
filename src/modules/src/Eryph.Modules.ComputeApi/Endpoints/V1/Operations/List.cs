using System.Threading;
using System.Threading.Tasks;
using Eryph.Modules.AspNetCore.ApiProvider;
using Eryph.Modules.AspNetCore.ApiProvider.Endpoints;
using Eryph.Modules.AspNetCore.ApiProvider.Handlers;
using Eryph.Modules.AspNetCore.ApiProvider.Model;
using Eryph.Modules.AspNetCore.ApiProvider.Model.V1;
using Eryph.StateDb.Model;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace Eryph.Modules.ComputeApi.Endpoints.V1.Operations;

public class List(
    IListRequestHandler<OperationsListRequest, Operation, OperationModel> listRequestHandler,
    IListEntitySpecBuilder<OperationsListRequest, OperationModel> specBuilder)
    : ListEntitiesEndpoint<OperationsListRequest, Operation, OperationModel>(
        listRequestHandler, specBuilder)
{
    [HttpGet("operations")]
    [SwaggerOperation(
            Summary = "List all operations",
            Description = "List all operations",
            OperationId = "Operations_List",
            Tags = ["Operations"]),
    ]
    [SwaggerResponse(
            StatusCodes.Status200OK,
            "Success",
            typeof(ListResponse<Operation>), "application/json"),
    ]
    public override Task<ActionResult<ListResponse<Operation>>> HandleAsync(
        [FromRoute] OperationsListRequest request,
        CancellationToken cancellationToken = default)
    {
        return base.HandleAsync(request, cancellationToken);
    }
}
