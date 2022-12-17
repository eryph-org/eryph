using System;
using System.Threading;
using System.Threading.Tasks;
using Eryph.Messages.Projects;
using Eryph.Messages.Resources.Catlets.Commands;
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
    public class Create : NewOperationRequestEndpoint<NewProjectRequest, Project> 
    {

        public Create([NotNull] ICreateEntityRequestHandler<Project> operationHandler) : base(operationHandler)
        {
        }

        [HttpPost("projects")]
        [SwaggerOperation(
            Summary = "Creates a new project",
            Description = "Creates a project",
            OperationId = "Projects_Create",
            Tags = new[] { "Projects" })
        ]
        public override Task<ActionResult<ListResponse<Operation>>> HandleAsync(
            [FromBody] NewProjectRequest request, CancellationToken cancellationToken = default)
        {
            return base.HandleAsync(request, cancellationToken);
        }


        protected override object CreateOperationMessage(NewProjectRequest request)
        {
            return new CreateProjectCommand
            {
                CorrelationId = request.CorrelationId.GetValueOrDefault(Guid.NewGuid()),
                Name = request.Name
            };
        }
    }
}
