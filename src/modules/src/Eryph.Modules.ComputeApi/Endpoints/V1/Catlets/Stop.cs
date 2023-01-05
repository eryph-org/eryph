using System.Threading;
using System.Threading.Tasks;
using Eryph.Messages.Resources.Catlets.Commands;
using Eryph.Modules.AspNetCore.ApiProvider;
using Eryph.Modules.AspNetCore.ApiProvider.Endpoints;
using Eryph.Modules.AspNetCore.ApiProvider.Handlers;
using Eryph.Modules.AspNetCore.ApiProvider.Model;
using Eryph.Resources;
using Eryph.StateDb.Model;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using Operation = Eryph.Modules.AspNetCore.ApiProvider.Model.V1.Operation;
using Resource = Eryph.Resources.Resource;

namespace Eryph.Modules.ComputeApi.Endpoints.V1.Catlets
{
    public class Stop : ResourceOperationEndpoint<SingleEntityRequest, Catlet>
    {


        public Stop([NotNull]IOperationRequestHandler<Catlet> operationHandler, 
            [NotNull] ISingleEntitySpecBuilder<SingleEntityRequest, Catlet> specBuilder) : base(operationHandler, specBuilder)
        {
        }

        protected override object CreateOperationMessage(Catlet model, SingleEntityRequest request)
        {
            return new StopCatletCommand{Resource = new Resource(ResourceType.Catlet, model.Id)};
        }

        [Authorize(Policy = "compute:catlets:stop")]
        [HttpPut("catlets/{id}/stop")]
        [SwaggerOperation(
            Summary = "Stops a catlet",
            Description = "Stops a catlet",
            OperationId = "Catlets_Stop",
            Tags = new[] { "Catlets" })
        ]

        public override Task<ActionResult<ListResponse<Operation>>> HandleAsync([FromRoute] SingleEntityRequest request, CancellationToken cancellationToken = default)
        {
            return base.HandleAsync(request, cancellationToken);
        }


    }
}
