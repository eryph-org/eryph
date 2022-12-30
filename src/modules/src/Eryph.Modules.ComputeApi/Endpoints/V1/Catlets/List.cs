using System.Threading;
using System.Threading.Tasks;
using Ardalis.Specification;
using Eryph.Modules.AspNetCore.ApiProvider;
using Eryph.Modules.AspNetCore.ApiProvider.Endpoints;
using Eryph.Modules.AspNetCore.ApiProvider.Handlers;
using Eryph.Modules.AspNetCore.ApiProvider.Model;
using Eryph.Modules.ComputeApi.Model.V1;
using Eryph.StateDb.Specifications;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace Eryph.Modules.ComputeApi.Endpoints.V1.Catlets
{
    public class List : ListEntityEndpoint<ListRequest,Catlet, StateDb.Model.Catlet>
    {
        public List([NotNull] IListRequestHandler<StateDb.Model.Catlet> listRequestHandler, 
            [NotNull] IListEntitySpecBuilder<ListRequest,StateDb.Model.Catlet> specBuilder) : base(listRequestHandler, specBuilder)
        {
        }

        [Authorize(Policy = "compute:catlets:read")]
        [HttpGet("catlets")]
        [SwaggerOperation(
            Summary = "List all catlets",
            Description = "List all catlets",
            OperationId = "Catlets_List",
            Tags = new[] { "Catlets" })
        ]
        [SwaggerResponse(Microsoft.AspNetCore.Http.StatusCodes.Status200OK, "Success", typeof(ListResponse<Catlet>))]
        public override Task<ActionResult<ListResponse<Catlet>>> HandleAsync([FromRoute] ListRequest request, CancellationToken cancellationToken = default)
        {
            return base.HandleAsync(request, cancellationToken);
        }


    }
}
