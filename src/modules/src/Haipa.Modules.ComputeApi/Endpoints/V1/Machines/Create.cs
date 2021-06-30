using System;
using System.ComponentModel.DataAnnotations;
using System.Threading;
using System.Threading.Tasks;
using Haipa.Messages.Resources.Machines.Commands;
using Haipa.Modules.AspNetCore.ApiProvider;
using Haipa.Modules.AspNetCore.ApiProvider.Endpoints;
using Haipa.Modules.AspNetCore.ApiProvider.Handlers;
using Haipa.Modules.AspNetCore.ApiProvider.Model;
using Haipa.Modules.AspNetCore.ApiProvider.Model.V1;
using Haipa.Resources.Machines.Config;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using Swashbuckle.AspNetCore.Annotations;

namespace Haipa.Modules.ComputeApi.Endpoints.V1.Machines
{
    public class Create : NewResourceOperationEndpoint<NewMachineRequest, StateDb.Model.Machine>
    {

        public Create([NotNull] INewResourceOperationHandler<StateDb.Model.Machine> operationHandler) : base(operationHandler)
        {
        }

        protected override object CreateOperationMessage(NewMachineRequest request)
        {
            var machineConfig = request.Configuration.ToObject<MachineConfig>();

            return new CreateMachineCommand{ CorrelationId = request.CorrelationId, Config = machineConfig };
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


    public class NewMachineRequest : RequestBase
    {
        [Required] public Guid CorrelationId { get; set; }

        [Required] public JObject Configuration { get; set; }
    }

}
