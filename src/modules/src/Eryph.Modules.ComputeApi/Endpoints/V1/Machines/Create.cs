using System.Threading;
using System.Threading.Tasks;
using Eryph.Messages.Resources.Machines.Commands;
using Eryph.Modules.AspNetCore.ApiProvider;
using Eryph.Modules.AspNetCore.ApiProvider.Endpoints;
using Eryph.Modules.AspNetCore.ApiProvider.Handlers;
using Eryph.Modules.AspNetCore.ApiProvider.Model;
using Eryph.Modules.AspNetCore.ApiProvider.Model.V1;
using Eryph.Resources.Machines.Config;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace Eryph.Modules.ComputeApi.Endpoints.V1.Machines
{
    public class Create : NewResourceOperationEndpoint<NewMachineRequest, StateDb.Model.Machine>
    {

        public Create([NotNull] INewResourceOperationHandler<StateDb.Model.Machine> operationHandler) : base(operationHandler)
        {
        }

        protected override object CreateOperationMessage(NewMachineRequest request)
        {

            return new CreateMachineCommand{ CorrelationId = request.CorrelationId, Config = request.Configuration };
        }
        
        [HttpPost("machines")]
        [SwaggerOperation(
            Summary = "Creates a new Machine",
            Description = "Creates a Machine",
            OperationId = "Machines_Create",
            Tags = new[] { "Machines" })
        ]

        public override Task<ActionResult<ListResponse<Operation>>> HandleAsync([FromBody] NewMachineRequest request, CancellationToken cancellationToken = default)
        {
            return base.HandleAsync(request, cancellationToken);
        }


    }
}
