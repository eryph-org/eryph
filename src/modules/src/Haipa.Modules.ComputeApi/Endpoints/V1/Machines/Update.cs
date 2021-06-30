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
using Haipa.Resources;
using Haipa.Resources.Machines.Config;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using Swashbuckle.AspNetCore.Annotations;

using static Microsoft.AspNetCore.Http.StatusCodes;

namespace Haipa.Modules.ComputeApi.Endpoints.V1.Machines
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

    public class UpdateMachineRequest : SingleResourceRequest
    {
        [FromBody] [Required] public Guid CorrelationId { get; set; }

        [FromBody] [Required] public JObject Configuration { get; set; }

    }
}
