using System;
using System.Threading;
using System.Threading.Tasks;
using Eryph.Modules.AspNetCore.ApiProvider;
using Eryph.Modules.AspNetCore.ApiProvider.Endpoints;
using Eryph.Modules.AspNetCore.ApiProvider.Handlers;
using Eryph.Modules.AspNetCore.ApiProvider.Model;
using Eryph.StateDb.Model;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using ProjectModel = Eryph.Modules.AspNetCore.ApiProvider.Model.V1.Project;

namespace Eryph.Modules.ComputeApi.Endpoints.V1.Projects
{
    public class Get : GetEntityEndpoint<SingleEntityRequest, ProjectModel, Project>
    {

        public Get([NotNull] IGetRequestHandler<Project, ProjectModel> requestHandler, 
            [NotNull] ISingleEntitySpecBuilder<SingleEntityRequest, Project> specBuilder) : base(requestHandler, specBuilder)
        {
        }

        [Authorize(Policy = "compute:projects:read")]
        [HttpGet("projects/{id}")]
        [SwaggerOperation(
            Summary = "Get a projects",
            Description = "Get a projects",
            OperationId = "Projects_Get",
            Tags = new[] { "Projects" })
        ]
        [SwaggerResponse(Microsoft.AspNetCore.Http.StatusCodes.Status200OK, "Success", typeof(ProjectModel))]
        public override async Task<ActionResult<ProjectModel>> HandleAsync([FromRoute] SingleEntityRequest request, CancellationToken cancellationToken = default)
        {
            return await base.HandleAsync(request, cancellationToken);
        }

    }
}
