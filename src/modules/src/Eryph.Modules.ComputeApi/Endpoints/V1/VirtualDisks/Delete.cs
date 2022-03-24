using System.Threading;
using System.Threading.Tasks;
using Eryph.Messages.Resources.Machines.Commands;
using Eryph.Modules.AspNetCore.ApiProvider.Endpoints;
using Eryph.Modules.AspNetCore.ApiProvider.Handlers;
using Eryph.Modules.AspNetCore.ApiProvider.Model;
using Eryph.Modules.AspNetCore.ApiProvider.Model.V1;
using Eryph.Resources;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;


namespace Eryph.Modules.ComputeApi.Endpoints.V1.VirtualDisks
{
    public class Delete : ResourceOperationEndpoint<SingleResourceRequest, StateDb.Model.VirtualDisk>
    {

        public Delete([NotNull] IResourceOperationHandler<StateDb.Model.VirtualDisk> operationHandler) : base(operationHandler)
        {
        }

        protected override object CreateOperationMessage(StateDb.Model.VirtualDisk model, SingleResourceRequest request)
        {
            return new DestroyMachineCommand{ Resource = new Resource(ResourceType.VirtualDisk, model.Id)};
        }


        [HttpDelete("virtualdisks/{id}")]
        [SwaggerOperation(
            Summary = "Deletes a virtual disk",
            Description = "Deletes a virtual disk",
            OperationId = "VirtualDisks_Delete",
            Tags = new[] { "Virtual Disks" })
        ]

        public override Task<ActionResult<ListResponse<Operation>>> HandleAsync([FromRoute] SingleResourceRequest request, CancellationToken cancellationToken = default)
        {
            return base.HandleAsync(request, cancellationToken);
        }


    }
}
