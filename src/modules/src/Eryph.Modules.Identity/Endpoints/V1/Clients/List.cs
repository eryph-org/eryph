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


namespace Eryph.Modules.Identity.Endpoints.V1.Clients
{
    [Route("v{version:apiVersion}")]
    public class List : EndpointBaseAsync
        .WithoutRequest
        .WithActionResult<ListResponse<Client>>
    {
        private readonly IClientService _clientService;
        private readonly IUserInfoProvider _userInfoProvider;

        public List(IClientService clientService, IUserInfoProvider userInfoProvider)
        {
            _clientService = clientService;
            _userInfoProvider = userInfoProvider;
        }


        [HttpGet("clients")]
        [Authorize("identity:clients:read")]
        [SwaggerOperation(
            Summary = "Lists clients",
            Description = "Lists clients",
            OperationId = "Clients_List",
            Tags = new[] { "Clients" })
        ]
        [SwaggerResponse(Status200OK, "Success", typeof(ListResponse<Client>))]
        public override async Task<ActionResult<ListResponse<Client>>> HandleAsync(CancellationToken cancellationToken = new CancellationToken())
        {
            var tenantId = _userInfoProvider.GetUserTenantId();

            var clients = await _clientService.List(tenantId, cancellationToken).Map(e => 
                e.Map(c => c.ToClient<Client>()));

            return new ActionResult<ListResponse<Client>>(new ListResponse<Client> { Value = clients });
        }
    }

}
