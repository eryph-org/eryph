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

namespace Eryph.Modules.ComputeApi.Endpoints.V1.Catlets
{
    public class Get : GetEntityEndpoint<SingleEntityRequest,Catlet, StateDb.Model.Catlet>
    {

        public Get([NotNull] IGetRequestHandler<StateDb.Model.Catlet, Catlet> requestHandler, [NotNull] 
            ISingleEntitySpecBuilder<SingleEntityRequest,StateDb.Model.Catlet> specBuilder) 
            : base(requestHandler, specBuilder)
        {
        }

        [HttpGet("catlets/{id}")]
        [SwaggerOperation(
            Summary = "Get a catlet",
            Description = "Get a catlet",
            OperationId = "Catlets_Get",
            Tags = new[] { "Catlets" })
        ]
        [SwaggerResponse(Microsoft.AspNetCore.Http.StatusCodes.Status200OK, "Success", typeof(Catlet))]

        public override Task<ActionResult<Catlet>> HandleAsync([FromRoute] SingleEntityRequest request, CancellationToken cancellationToken = default)
        {
            var id = HttpContext.TraceIdentifier;
            return base.HandleAsync(request, cancellationToken);
        }

    }
}
