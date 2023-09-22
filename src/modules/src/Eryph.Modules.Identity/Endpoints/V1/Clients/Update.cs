using System.Threading;
using System.Threading.Tasks;
using Ardalis.ApiEndpoints;
using Eryph.Modules.AspNetCore;
using Eryph.Modules.Identity.Models;
using Eryph.Modules.Identity.Models.V1;
using Eryph.Modules.Identity.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Abstractions;
using Swashbuckle.AspNetCore.Annotations;

using static Microsoft.AspNetCore.Http.StatusCodes;


namespace Eryph.Modules.Identity.Endpoints.V1.Clients
{
    [Route("v{version:apiVersion}")]

    public class Update : EndpointBaseAsync
        .WithRequest<UpdateClientRequest>
        .WithActionResult<Client>
    {
        private readonly IClientService _clientService;
        private readonly IUserInfoProvider _userInfoProvider;
        private readonly IOpenIddictScopeManager _scopeManager;

        public Update(IClientService clientService, IOpenIddictScopeManager scopeManager, IUserInfoProvider userInfoProvider)
        {
            _clientService = clientService;
            _scopeManager = scopeManager;
            _userInfoProvider = userInfoProvider;
        }


        [Authorize(Policy = "identity:clients:write")]
        [HttpPut("clients/{id}")]
        [SwaggerOperation(
            Summary = "Updates a client",
            Description = "Updates a client",
            OperationId = "Clients_Update",
            Tags = new[] { "Clients" })
        ]
        [SwaggerResponse(Status200OK, "Success", typeof(Client))]

        public override async Task<ActionResult<Client>> HandleAsync([FromRoute] UpdateClientRequest request, CancellationToken cancellationToken = new CancellationToken())
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var tenantId = _userInfoProvider.GetUserTenantId();
            var (scopesValid, invalidScope) = await request.Client.ValidateScopes(_scopeManager, cancellationToken);
            if (!scopesValid)
                return BadRequest($"Invalid scope {invalidScope}.");

            var persistentDescriptor = await _clientService.Get(request.Id, tenantId, cancellationToken);
            if (persistentDescriptor == null)
                return NotFound($"client with id {request.Id} not found.");

            persistentDescriptor.Scopes.Clear();
            persistentDescriptor.Scopes.UnionWith(request.Client.AllowedScopes);
            persistentDescriptor.DisplayName = request.Client.Name;

            await _clientService.Update(persistentDescriptor, cancellationToken);


            return Ok(persistentDescriptor);

        }
    }
}
