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
using VirtualCatlet = Eryph.StateDb.Model.VirtualCatlet;

namespace Eryph.Modules.ComputeApi.Endpoints.V1.VirtualCatlets
{
    public class GetConfig : GetEntityEndpoint<SingleEntityRequest,VirtualCatletConfiguration, VirtualCatlet>
    {

        public GetConfig([NotNull] IGetRequestHandler<VirtualCatlet, VirtualCatletConfiguration> requestHandler, 
            [NotNull]ISingleEntitySpecBuilder<SingleEntityRequest,VirtualCatlet> specBuilder) : base(requestHandler, specBuilder)
        {
        }

        [HttpGet("vcatlets/{id}/config")]
        [SwaggerOperation(
            Summary = "Get virtual catlet configuration",
            Description = "Get the configuration of a virtual catlet",
            OperationId = "VCatlets_GetConfig",
            Tags = new[] { "Virtual Catlets" })
        ]
        [SwaggerResponse(Microsoft.AspNetCore.Http.StatusCodes.Status200OK, "Success", typeof(VirtualCatlet))]

        public override Task<ActionResult<VirtualCatletConfiguration>> HandleAsync([FromRoute] SingleEntityRequest request, CancellationToken cancellationToken = default)
        {
            return base.HandleAsync(request, cancellationToken);
        }



    }
}
