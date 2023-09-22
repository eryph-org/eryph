using System.Threading;
using System.Threading.Tasks;
using Ardalis.ApiEndpoints;
using Eryph.Modules.AspNetCore;
using Eryph.Modules.Identity.Models.V1;
using Eryph.Modules.Identity.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

using static Microsoft.AspNetCore.Http.StatusCodes;


namespace Eryph.Modules.Identity.Endpoints.V1.Clients
{
    [Route("v{version:apiVersion}")]
    public class Delete : EndpointBaseAsync
        .WithRequest<DeleteClientRequest>
        .WithoutResult
    {
        private readonly IClientService _clientService;
        private readonly IUserInfoProvider _userInfoProvider;

        public Delete(IClientService clientService, IUserInfoProvider userInfoProvider)
        {
            _clientService = clientService;
            _userInfoProvider = userInfoProvider;
        }


        [Authorize(Policy = "identity:clients:write")]
        [HttpDelete("clients/{id}")]
        [SwaggerOperation(
            Summary = "Deletes a client",
            Description = "Deletes a client",
            OperationId = "Clients_Delete",
            Tags = new[] { "Clients" })
        ]
        [ProducesResponseType(Status200OK)]
        public override async Task<ActionResult> HandleAsync([FromRoute] DeleteClientRequest request, CancellationToken cancellationToken = new CancellationToken())
        {
            var tenantId = _userInfoProvider.GetUserTenantId();

            var client = await _clientService.Get(request.Id, tenantId, cancellationToken);
            if (client == null)
                return NotFound($"client with id {request.Id} not found.");

            await _clientService.Delete(client.ClientId, tenantId, cancellationToken);

            return Ok();
        }
    }
}
