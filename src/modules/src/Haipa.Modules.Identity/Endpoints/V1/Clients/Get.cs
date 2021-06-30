using System.Threading;
using System.Threading.Tasks;
using Ardalis.ApiEndpoints;
using Haipa.Modules.Identity.Models.V1;
using Haipa.Modules.Identity.Services;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

using static Microsoft.AspNetCore.Http.StatusCodes;


namespace Haipa.Modules.Identity.Endpoints.V1.Clients
{
    [Route("v{version:apiVersion}")]
    public class Get : BaseAsyncEndpoint
        .WithRequest<GetClientRequest>
        .WithResponse<Client>
    {
        private readonly IClientService<Client> _clientService;

        public Get(IClientService<Client> clientService)
        {
            _clientService = clientService;
        }


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
            var client = await _clientService.GetClient(request.Id);

            if (client == null)
                return NotFound($"client with id {request.Id} not found.");

            return Ok(client);
        }
    }

    public class GetClientRequest
    {
        [FromRoute(Name = "id")] public string Id { get; set; }
    }
}
