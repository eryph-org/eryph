using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ardalis.ApiEndpoints;
using Eryph.Modules.AspNetCore;
using Eryph.Modules.AspNetCore.ApiProvider.Model;
using Eryph.Modules.Identity.Models;
using Eryph.Modules.Identity.Models.V1;
using Eryph.Modules.Identity.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace Eryph.Modules.Identity.Endpoints.V1.Clients;

[Route("v{version:apiVersion}")]
public class List(
    IClientService clientService,
    IUserInfoProvider userInfoProvider)
    : EndpointBaseAsync
        .WithoutRequest
        .WithActionResult<ListResponse<Client>>
{
    [HttpGet("clients")]
    [Authorize("identity:clients:read")]
    [SwaggerOperation(
        Summary = "List all clients",
        Description = "List all clients",
        OperationId = "Clients_List",
        Tags = ["Clients"])
    ]
    [SwaggerResponse(
        statusCode: StatusCodes.Status200OK,
        description: "Success",
        type: typeof(ListResponse<Client>),
        contentTypes: ["application/json"])
    ]
    public override async Task<ActionResult<ListResponse<Client>>> HandleAsync(
        CancellationToken cancellationToken = default)
    {
        var tenantId = userInfoProvider.GetUserTenantId();

        var clientDescriptors = await clientService.List(tenantId, cancellationToken);
        var clients = clientDescriptors.Map(d => d.ToClient()).ToList();

        return Ok(new ListResponse<Client> { Value = clients });
    }
}
