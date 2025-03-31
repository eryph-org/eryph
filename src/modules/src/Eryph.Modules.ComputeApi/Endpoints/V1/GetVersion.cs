using System.Threading;
using System.Threading.Tasks;
using Ardalis.ApiEndpoints;
using Eryph.Modules.AspNetCore.ApiProvider.Model.V1;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace Eryph.Modules.ComputeApi.Endpoints.V1;

[Route("v{version:apiVersion}")]
public class GetVersion : EndpointBaseAsync
    .WithoutRequest
    .WithActionResult<ApiVersionResponse>
{
    [HttpGet("version")]
    [SwaggerOperation(
        Summary = "Get the API version",
        Description = "Gets the API version which can be used by clients for compatibility checks. "
        + "This endpoint has been added with eryph v0.3.",
        OperationId = "Version_Get",
        Tags = ["Version"])
    ]
    [SwaggerResponse(
        statusCode: StatusCodes.Status200OK,
        description: "Success",
        type: typeof(ApiVersionResponse),
        contentTypes: ["application/json"])
    ]
    public override Task<ActionResult<ApiVersionResponse>> HandleAsync(
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new ActionResult<ApiVersionResponse>(new ApiVersionResponse
        {
            LatestVersion = new ApiVersion()
            {
                Major = ComputeApiVersion.Major,
                Minor = ComputeApiVersion.Minor,
            }
        }));
}
