using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ardalis.ApiEndpoints;
using Eryph.Modules.AspNetCore.ApiProvider.Model;
using Eryph.Modules.Identity.Models.V1;
using Eryph.Modules.Identity.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

using static Microsoft.AspNetCore.Http.StatusCodes;


namespace Eryph.Modules.Identity.Endpoints.V1.Clients
{
    [Route("v{version:apiVersion}")]
    public class List : EndpointBaseAsync
        .WithoutRequest
        .WithActionResult<ListResponse<Client>>
    {
        private readonly IClientService<Client> _clientService;

        public List(IClientService<Client> clientService)
        {
            _clientService = clientService;
        }


        [HttpGet("clients")]
        [Authorize("identity:clients:read:all")]
        [SwaggerOperation(
            Summary = "Lists clients",
            Description = "Lists clients",
            OperationId = "Clients_List",
            Tags = new[] { "Clients" })
        ]
        [SwaggerResponse(Status200OK, "Success", typeof(ListResponse<Client>))]
        public override async Task<ActionResult<ListResponse<Client>>> HandleAsync(CancellationToken cancellationToken = new CancellationToken())
        {
            var clients = _clientService.QueryClients().ToArray();
            return new (new ListResponse<Client> { Value = clients });
        }
    }

}
