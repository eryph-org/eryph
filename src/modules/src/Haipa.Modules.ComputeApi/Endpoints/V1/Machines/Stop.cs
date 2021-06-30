using System.Threading;
using System.Threading.Tasks;
using Haipa.Messages.Resources.Machines.Commands;
using Haipa.Modules.AspNetCore.ApiProvider;
using Haipa.Modules.AspNetCore.ApiProvider.Endpoints;
using Haipa.Modules.AspNetCore.ApiProvider.Handlers;
using Haipa.Modules.AspNetCore.ApiProvider.Model;
using Haipa.Modules.AspNetCore.ApiProvider.Model.V1;
using Haipa.Resources;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

using static Microsoft.AspNetCore.Http.StatusCodes;

namespace Haipa.Modules.ComputeApi.Endpoints.V1.Machines
{
    public class Stop : ResourceOperationEndpoint<SingleResourceRequest, StateDb.Model.Machine>
    {

        public Stop([NotNull] IResourceOperationHandler<StateDb.Model.Machine> operationHandler) : base(operationHandler)
        {
        }

        protected override object CreateOperationMessage(StateDb.Model.Machine model, SingleResourceRequest request)
        {
            return new StartMachineCommand{Resource = new Resource(ResourceType.Machine, model.Id)};
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
