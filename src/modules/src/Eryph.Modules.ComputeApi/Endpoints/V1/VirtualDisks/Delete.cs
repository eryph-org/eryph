using System.Threading;
using System.Threading.Tasks;
using Eryph.Messages.Resources.Catlets.Commands;
using Eryph.Modules.AspNetCore.ApiProvider;
using Eryph.Modules.AspNetCore.ApiProvider.Endpoints;
using Eryph.Modules.AspNetCore.ApiProvider.Handlers;
using Eryph.Modules.AspNetCore.ApiProvider.Model;
using Eryph.StateDb.Model;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using Operation = Eryph.Modules.AspNetCore.ApiProvider.Model.V1.Operation;


namespace Eryph.Modules.ComputeApi.Endpoints.V1.VirtualDisks
{
    public class Delete : ResourceOperationEndpoint<SingleEntityRequest, VirtualDisk>
    {

        public Delete([NotNull] IOperationRequestHandler<VirtualDisk> operationHandler, 
            [NotNull] ISingleEntitySpecBuilder<SingleEntityRequest, VirtualDisk> specBuilder) : base(operationHandler, specBuilder)
        {
        }

        protected override object CreateOperationMessage(VirtualDisk model, SingleEntityRequest request)
        {
            return new DestroyVirtualDiskCommand{ DiskId = model.Id };
        }


        [HttpDelete("virtualdisks/{id}")]
        [SwaggerOperation(
            Summary = "Deletes a virtual disk",
            Description = "Deletes a virtual disk",
            OperationId = "VirtualDisks_Delete",
            Tags = new[] { "Virtual Disks" })
        ]

        public override Task<ActionResult<ListResponse<Operation>>> HandleAsync([FromRoute] SingleEntityRequest request, CancellationToken cancellationToken = default)
        {
            return base.HandleAsync(request, cancellationToken);
        }


    }
}
