using System;
using System.Threading;
using System.Threading.Tasks;
using Eryph.Messages.Projects;
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

namespace Eryph.Modules.CommonApi.Endpoints.V1.Projects
{
    public class Update : OperationRequestEndpoint<UpdateProjectRequest, Project>
    {
        public Update([NotNull] IOperationRequestHandler<Project> operationHandler,
            [NotNull] ISingleEntitySpecBuilder<SingleEntityRequest, Project> specBuilder) : base(operationHandler, specBuilder)
        {
        }

        [HttpPut("projects/{id}")]
        [SwaggerOperation(
            Summary = "Updates a project",
            Description = "Updates a project",
            OperationId = "Projects_Update",
            Tags = new[] { "Projects" })
        ]
        public override Task<ActionResult<ListResponse<Operation>>> HandleAsync([FromRoute] UpdateProjectRequest request, CancellationToken cancellationToken = default)
        {
            return base.HandleAsync(request, cancellationToken);
        }

        protected override object CreateOperationMessage(Project model, UpdateProjectRequest request)
        {
            return new UpdateProjectCommand
            {
                ProjectId = Guid.Parse(request.Id),
                CorrelationId = request.Body.CorrelationId.GetValueOrDefault(Guid.NewGuid()),
                Name = request.Body.Name
            };
        }
    }
}
