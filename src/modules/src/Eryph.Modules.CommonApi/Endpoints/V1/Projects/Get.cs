using System.Threading;
using System.Threading.Tasks;
using Eryph.Modules.AspNetCore.ApiProvider;
using Eryph.Modules.AspNetCore.ApiProvider.Endpoints;
using Eryph.Modules.AspNetCore.ApiProvider.Handlers;
using Eryph.Modules.AspNetCore.ApiProvider.Model;
using Eryph.StateDb.Model;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using ProjectModel = Eryph.Modules.AspNetCore.ApiProvider.Model.V1.Project;

namespace Eryph.Modules.CommonApi.Endpoints.V1.Projects
{
    public class Get : GetEntityEndpoint<SingleEntityRequest, ProjectModel, Project>
    {

        public Get([NotNull] IGetRequestHandler<Project, ProjectModel> requestHandler, 
            [NotNull] ISingleEntitySpecBuilder<SingleEntityRequest, Project> specBuilder) : base(requestHandler, specBuilder)
        {
        }

        [HttpGet("projects/{id}")]
        [SwaggerOperation(
            Summary = "Get a projects",
            Description = "Get a projects",
            OperationId = "Projects_Get",
            Tags = new[] { "Projects" })
        ]
        [SwaggerResponse(Microsoft.AspNetCore.Http.StatusCodes.Status200OK, "Success", typeof(ProjectModel))]
        public override Task<ActionResult<ProjectModel>> HandleAsync([FromRoute] SingleEntityRequest request, CancellationToken cancellationToken = default)
        {
            return base.HandleAsync(request, cancellationToken);
        }

    }
}
