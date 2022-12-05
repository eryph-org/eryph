using System.Threading;
using System.Threading.Tasks;
using Eryph.Messages.Resources.Catlets.Commands;
using Eryph.Modules.AspNetCore.ApiProvider.Endpoints;
using Eryph.Modules.AspNetCore.ApiProvider.Handlers;
using Eryph.Modules.AspNetCore.ApiProvider.Model;
using Eryph.Modules.AspNetCore.ApiProvider.Model.V1;
using Eryph.Resources;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace Eryph.Modules.ComputeApi.Endpoints.V1.Catlets
{
    public class Start : ResourceOperationEndpoint<SingleResourceRequest, StateDb.Model.Catlet>
    {

        public Start([NotNull] IResourceOperationHandler<StateDb.Model.Catlet> operationHandler) : base(operationHandler)
        {
        }

        protected override object CreateOperationMessage(StateDb.Model.Catlet model, SingleResourceRequest request)
        {
            return new StartCatletCommand{Resource = new Resource(ResourceType.Machine, model.Id)};
        }


        [HttpPut("catlets/{id}/start")]
        [SwaggerOperation(
            Summary = "Starts a catlet",
            Description = "Starts a catlet",
            OperationId = "Catlets_Start",
            Tags = new[] { "Catlets" })
        ]

        public override Task<ActionResult<ListResponse<Operation>>> HandleAsync([FromRoute] SingleResourceRequest request, CancellationToken cancellationToken = default)
        {
            return base.HandleAsync(request, cancellationToken);
        }


    }
}
