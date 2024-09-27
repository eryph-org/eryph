using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ardalis.ApiEndpoints;
using Eryph.Modules.AspNetCore;
using Eryph.Modules.AspNetCore.ApiProvider.Model;
using Eryph.Modules.Identity.Models;
using Eryph.Modules.Identity.Models.V1;
using Eryph.Modules.Identity.Services;
using LanguageExt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

using static Microsoft.AspNetCore.Http.StatusCodes;

namespace Eryph.Modules.Identity.Endpoints.V1.Clients;

[Route("v{version:apiVersion}")]
public class List(
    IClientService clientService,
    IUserInfoProvider userInfoProvider)
    : EndpointBaseAsync
        .WithoutRequest
        .WithActionResult<ListEntitiesResponse<Client>>
{
    [HttpGet("clients")]
    [Authorize("identity:clients:read")]
    [SwaggerOperation(
        Summary = "Lists clients",
        Description = "Lists clients",
        OperationId = "Clients_List",
        Tags = ["Clients"])
    ]
    [SwaggerResponse(Status200OK, "Success", typeof(ListEntitiesResponse<Client>))]
    public override async Task<ActionResult<ListEntitiesResponse<Client>>> HandleAsync(
        CancellationToken cancellationToken = default)
    {
        var tenantId = userInfoProvider.GetUserTenantId();

        var clientDescriptors = await clientService.List(tenantId, cancellationToken);
        var clients = clientDescriptors.Map(d => d.ToClient<Client>()).ToList();

        return Ok(new ListEntitiesResponse<Client> { Value = clients });
    }
}
