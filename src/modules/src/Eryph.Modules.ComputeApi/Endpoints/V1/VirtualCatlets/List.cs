using System.Threading;
using System.Threading.Tasks;
using Eryph.Modules.AspNetCore.ApiProvider;
using Eryph.Modules.AspNetCore.ApiProvider.Endpoints;
using Eryph.Modules.AspNetCore.ApiProvider.Handlers;
using Eryph.Modules.AspNetCore.ApiProvider.Model;
using Eryph.Modules.ComputeApi.Model.V1;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace Eryph.Modules.ComputeApi.Endpoints.V1.VirtualCatlets
{
    public class List : ListResourceEndpoint<ListRequest, VirtualCatlet, StateDb.Model.VirtualCatlet>
    {
        public List([NotNull] IListRequestHandler<StateDb.Model.VirtualCatlet> listRequestHandler, [NotNull] IListResourceSpecBuilder<StateDb.Model.VirtualCatlet> specBuilder) : base(listRequestHandler, specBuilder)
        {

        }

        [HttpGet("vcatlets")]
        [SwaggerOperation(
            Summary = "Get list of virtual catlets",
            Description = "Get list of virtual catlets",
            OperationId = "VCatlets_List",
            Tags = new[] { "Virtual Catlets" })
        ]
        [SwaggerResponse(Microsoft.AspNetCore.Http.StatusCodes.Status200OK, "Success", typeof(ListResponse<VirtualCatlet>))]
        public override Task<ActionResult<ListResponse<VirtualCatlet>>> HandleAsync([FromRoute] ListRequest request, CancellationToken cancellationToken = default)
        {
            return base.HandleAsync(request, cancellationToken);
        }


    }
}
