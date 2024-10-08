using System.Threading;
using System.Threading.Tasks;
using Eryph.Modules.AspNetCore.ApiProvider;
using Eryph.Modules.AspNetCore.ApiProvider.Endpoints;
using Eryph.Modules.AspNetCore.ApiProvider.Handlers;
using Eryph.Modules.AspNetCore.ApiProvider.Model;
using Eryph.Modules.AspNetCore.ApiProvider.Model.V1;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace Eryph.Modules.ComputeApi.Endpoints.V1.Operations;

public class List(
    IListRequestHandler<OperationsListRequest, Operation, StateDb.Model.OperationModel> listRequestHandler,
    IListEntitySpecBuilder<OperationsListRequest, StateDb.Model.OperationModel> specBuilder)
    : ListEntitiesEndpoint<OperationsListRequest, Operation, StateDb.Model.OperationModel>(
        listRequestHandler, specBuilder)
{
    [HttpGet("operations")]
    [SwaggerOperation(
        Summary = "List all operations",
        Description = "List all operations",
        OperationId = "Operations_List",
        Tags = ["Operations"])
    ]
    [SwaggerResponse(
        statusCode: StatusCodes.Status200OK,
        description: "Success",
        type: typeof(ListResponse<Operation>),
        contentTypes: ["application/json"])
    ]
    public override Task<ActionResult<ListResponse<Operation>>> HandleAsync(
        [FromRoute] OperationsListRequest request,
        CancellationToken cancellationToken = default)
    {
        return base.HandleAsync(request, cancellationToken);
    }
}
