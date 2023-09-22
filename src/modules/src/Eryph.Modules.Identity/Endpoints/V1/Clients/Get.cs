using System.Threading;
using System.Threading.Tasks;
using Ardalis.ApiEndpoints;
using Eryph.Modules.AspNetCore;
using Eryph.Modules.Identity.Models;
using Eryph.Modules.Identity.Models.V1;
using Eryph.Modules.Identity.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

using static Microsoft.AspNetCore.Http.StatusCodes;


namespace Eryph.Modules.Identity.Endpoints.V1.Clients
{
    [Route("v{version:apiVersion}")]
    public class Get : EndpointBaseAsync
        .WithRequest<GetClientRequest>
        .WithActionResult<Client>
    {
        private readonly IClientService _clientService;
        private readonly IUserInfoProvider _userInfoProvider;
        public Get(IClientService clientService, IUserInfoProvider userInfoProvider)
        {
            _clientService = clientService;
            _userInfoProvider = userInfoProvider;
        }

        [Authorize(Policy = "identity:clients:read")]
        [HttpGet("clients/{id}")]
        [SwaggerOperation(
            Summary = "Get a client",
            Description = "Get a client",
            OperationId = "Clients_Get",
            Tags = new[] { "Clients" })
        ]
        [SwaggerResponse(Status200OK, "Success", typeof(Client))]

        public override async Task<ActionResult<Client>> HandleAsync([FromRoute] GetClientRequest request, CancellationToken cancellationToken = new CancellationToken())
        {
            var tenantId = _userInfoProvider.GetUserTenantId();
            var descriptor = await _clientService.Get(request.Id, tenantId, cancellationToken);

            if (descriptor == null)
                return NotFound($"client with id {request.Id} not found.");

            var client = descriptor.ToClient<Client>();
            return Ok(client);
        }
    }
}
