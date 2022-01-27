using System.Threading;
using System.Threading.Tasks;
using Eryph.Messages.Resources.Machines.Commands;
using Eryph.Modules.AspNetCore.ApiProvider;
using Eryph.Modules.AspNetCore.ApiProvider.Endpoints;
using Eryph.Modules.AspNetCore.ApiProvider.Handlers;
using Eryph.Modules.AspNetCore.ApiProvider.Model;
using Eryph.Modules.AspNetCore.ApiProvider.Model.V1;
using Eryph.Resources;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

using static Microsoft.AspNetCore.Http.StatusCodes;

namespace Eryph.Modules.ComputeApi.Endpoints.V1.Machines
{
    public class Stop : ResourceOperationEndpoint<SingleResourceRequest, StateDb.Model.Machine>
    {

        public Stop([NotNull] IResourceOperationHandler<StateDb.Model.Machine> operationHandler) : base(operationHandler)
        {
        }

        protected override object CreateOperationMessage(StateDb.Model.Machine model, SingleResourceRequest request)
        {
            return new StopMachineCommand{Resource = new Resource(ResourceType.Machine, model.Id)};
        }


        [HttpPut("machines/{id}/stop")]
        [SwaggerOperation(
            Summary = "Stops a Machine",
            Description = "Stops a Machine",
            OperationId = "Machines_Stop",
            Tags = new[] { "Machines" })
        ]

        public override Task<ActionResult<ListResponse<Operation>>> HandleAsync([FromRoute] SingleResourceRequest request, CancellationToken cancellationToken = default)
        {
            return base.HandleAsync(request, cancellationToken);
        }


    }
}
