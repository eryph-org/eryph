using System.Threading;
using System.Threading.Tasks;
using Ardalis.ApiEndpoints;
using Eryph.Modules.AspNetCore;
using Eryph.Modules.Identity.Models;
using Eryph.Modules.Identity.Models.V1;
using Eryph.Modules.Identity.Services;
using Eryph.Security.Cryptography;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

using static Microsoft.AspNetCore.Http.StatusCodes;


namespace Eryph.Modules.Identity.Endpoints.V1.Clients
{
    [Route("v{version:apiVersion}")]
    public class NewKey : EndpointBaseAsync
        .WithRequest<NewClientKeyRequest>
        .WithActionResult<ClientWithSecret>
    {
        private readonly IClientService _clientService;
        private readonly ICertificateGenerator _certificateGenerator;
        private readonly IUserInfoProvider _userInfoProvider;

        public NewKey(IClientService clientService, ICertificateGenerator certificateGenerator, IUserInfoProvider userInfoProvider)
        {
            _clientService = clientService;
            _certificateGenerator = certificateGenerator;
            _userInfoProvider = userInfoProvider;
        }


        [Authorize(Policy = "identity:clients:write")]
        [HttpPost("clients/{id}/newkey")]
        [SwaggerOperation(
            Summary = "Updates a client key",
            Description = "Updates a client key",
            OperationId = "Clients_NewKey",
            Tags = new[] { "Clients" })
        ]
        [SwaggerResponse(Status200OK, "Success", typeof(ClientWithSecret))]

        public override async Task<ActionResult<ClientWithSecret>> HandleAsync([FromRoute] NewClientKeyRequest request, CancellationToken cancellationToken = new CancellationToken())
        {
            var tenantId = _userInfoProvider.GetUserTenantId();
            var descriptor = await _clientService.Get(request.Id, tenantId, cancellationToken);
            if (descriptor == null)
                return NotFound($"client with id {request.Id} not found.");


            var privateKey = await descriptor.NewClientCertificate(_certificateGenerator);
            descriptor = await _clientService.Update(descriptor, cancellationToken);

            var createdClient = descriptor.ToClient<ClientWithSecret>();
            createdClient.Key = privateKey;

            return Ok(createdClient);
        }
    }
}
