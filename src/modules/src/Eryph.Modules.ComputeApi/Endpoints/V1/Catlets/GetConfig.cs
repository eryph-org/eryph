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
using Catlet = Eryph.StateDb.Model.Catlet;

namespace Eryph.Modules.ComputeApi.Endpoints.V1.Catlets
{
    public class GetConfig : GetEntityEndpoint<SingleEntityRequest, CatletConfiguration, Catlet>
    {

        public GetConfig([NotNull] IGetRequestHandler<Catlet, CatletConfiguration> requestHandler,
            [NotNull] ISingleEntitySpecBuilder<SingleEntityRequest, Catlet> specBuilder) : base(requestHandler, specBuilder)
        {
        }

        [HttpGet("catlets/{id}/config")]
        [SwaggerOperation(
            Summary = "Get catlet configuration",
            Description = "Get the configuration of a catlet",
            OperationId = "Catlets_GetConfig",
            Tags = new[] { "Catlets" })
        ]

        [SwaggerResponse(Microsoft.AspNetCore.Http.StatusCodes.Status200OK, "Success", typeof(CatletConfiguration))]

        public override Task<ActionResult<CatletConfiguration>> HandleAsync([FromRoute] SingleEntityRequest request, CancellationToken cancellationToken = default)
        {
            return base.HandleAsync(request, cancellationToken);
        }



    }
}
