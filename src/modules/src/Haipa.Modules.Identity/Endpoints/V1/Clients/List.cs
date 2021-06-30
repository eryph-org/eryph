using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ardalis.ApiEndpoints;
using Haipa.Modules.AspNetCore.ApiProvider.Model;
using Haipa.Modules.Identity.Models.V1;
using Haipa.Modules.Identity.Services;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Swashbuckle.AspNetCore.Annotations;

using static Microsoft.AspNetCore.Http.StatusCodes;


namespace Haipa.Modules.Identity.Endpoints.V1.Clients
{
    [Route("v{version:apiVersion}")]

    public class List : BaseAsyncEndpoint
        .WithoutRequest
        .WithResponse<ListResponse<Client>>
    {
        private readonly IClientService<Client> _clientService;

        public List(IClientService<Client> clientService)
        {
            _clientService = clientService;
        }


        [HttpGet("clients")]
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
            return new JsonResult(new ListResponse<Client> { Value = clients }, new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore,
            });
        }
    }

}
