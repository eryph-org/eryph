using System.Threading;
using System.Threading.Tasks;
using Eryph.Messages.Resources.Machines.Commands;
using Eryph.Modules.AspNetCore.ApiProvider;
using Eryph.Modules.AspNetCore.ApiProvider.Endpoints;
using Eryph.Modules.AspNetCore.ApiProvider.Handlers;
using Eryph.Modules.AspNetCore.ApiProvider.Model;
using Eryph.Modules.AspNetCore.ApiProvider.Model.V1;
using Eryph.Resources;
using Eryph.Resources.Machines.Config;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

using static Microsoft.AspNetCore.Http.StatusCodes;

namespace Eryph.Modules.ComputeApi.Endpoints.V1.Machines
{
    public class Update : ResourceOperationEndpoint<UpdateMachineRequest, StateDb.Model.Machine>
    {

        public Update([NotNull] IResourceOperationHandler<StateDb.Model.Machine> operationHandler) : base(operationHandler)
        {
        }

        protected override object CreateOperationMessage(StateDb.Model.Machine model, UpdateMachineRequest request )
        {
            var machineConfig = request.Configuration.ToObject<MachineConfig>();

            return new UpdateMachineCommand(){Resource = new Resource(ResourceType.Machine, model.Id), 
                CorrelationId = request.CorrelationId, Config = machineConfig};
        }


        [HttpPut("machines/{id}")]
        [SwaggerOperation(
            Summary = "Updates a Machine",
            Description = "Updates a Machine",
            OperationId = "Machines_Update",
            Tags = new[] { "Machines" })
        ]

        public override Task<ActionResult<ListResponse<Operation>>> HandleAsync([FromRoute] UpdateMachineRequest request, CancellationToken cancellationToken = default)
        {
            return base.HandleAsync(request, cancellationToken);
        }


    }
}
