using System.Threading;
using System.Threading.Tasks;
using Eryph.Modules.AspNetCore.ApiProvider;
using Eryph.Modules.AspNetCore.ApiProvider.Endpoints;
using Eryph.Modules.AspNetCore.ApiProvider.Handlers;
using Eryph.Modules.AspNetCore.ApiProvider.Model;
using Eryph.Modules.ComputeApi.Model.V1;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace Eryph.Modules.ComputeApi.Endpoints.V1.VirtualCatlets
{
    public class Get : GetResourceEndpoint<VirtualCatlet, StateDb.Model.VirtualCatlet>
    {

        public Get([NotNull] IGetRequestHandler<StateDb.Model.VirtualCatlet> requestHandler, 
            [NotNull] ISingleResourceSpecBuilder<StateDb.Model.VirtualCatlet> specBuilder) : base(requestHandler, specBuilder)
        {
        }

        [HttpGet("vcatlets/{id}")]
        [SwaggerOperation(
            Summary = "Get a virtual catlet",
            Description = "Get a virtual catlet",
            OperationId = "VCatlets_Get",
            Tags = new[] { "Virtual Catlets" })
        ]
        [SwaggerResponse(Microsoft.AspNetCore.Http.StatusCodes.Status200OK, "Success", typeof(VirtualCatlet))]

        public override Task<ActionResult<VirtualCatlet>> HandleAsync([FromRoute] SingleResourceRequest request, CancellationToken cancellationToken = default)
        {
            return base.HandleAsync(request, cancellationToken);
        }

    }
}
