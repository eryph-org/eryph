using System.Threading;
using System.Threading.Tasks;
using Eryph.Modules.AspNetCore.ApiProvider;
using Eryph.Modules.AspNetCore.ApiProvider.Endpoints;
using Eryph.Modules.AspNetCore.ApiProvider.Handlers;
using Eryph.Modules.AspNetCore.ApiProvider.Model;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

using ProjectModel = Eryph.Modules.AspNetCore.ApiProvider.Model.V1.Project;

namespace Eryph.Modules.CommonApi.Endpoints.V1.Projects
{
    public class List : ListEntityEndpoint<ProjectsListRequest, ProjectModel, StateDb.Model.Project>
    {
        public List([NotNull]IListRequestHandler<StateDb.Model.Project> listRequestHandler, 
            [NotNull] IListEntitySpecBuilder<ProjectsListRequest, StateDb.Model.Project> specBuilder) : base(listRequestHandler, specBuilder)
        {
        }
        
        [HttpGet("projects")]
        [SwaggerOperation(
            Summary = "List all projects",
            Description = "List all projects",
            OperationId = "Projects_List",
            Tags = new[] { "Projects" })
        ]
        [SwaggerResponse(Microsoft.AspNetCore.Http.StatusCodes.Status200OK, "Success", typeof(ListResponse<ProjectModel>))]
        public override Task<ActionResult<ListResponse<ProjectModel>>> HandleAsync([FromRoute] ProjectsListRequest request, CancellationToken cancellationToken = default)
        {
            return base.HandleAsync(request, cancellationToken);
        }


    }
}
