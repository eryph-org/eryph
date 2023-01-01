using System;
using System.Threading;
using System.Threading.Tasks;
using Eryph.ConfigModel.Catlets;
using Eryph.ConfigModel.Json;
using Eryph.Messages.Resources.Networks.Commands;
using Eryph.Modules.AspNetCore.ApiProvider.Endpoints;
using Eryph.Modules.AspNetCore.ApiProvider.Handlers;
using Eryph.Modules.AspNetCore.ApiProvider.Model;
using Eryph.Modules.AspNetCore.ApiProvider.Model.V1;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace Eryph.Modules.ComputeApi.Endpoints.V1.VirtualNetworks
{
    public class Update : NewOperationRequestEndpoint<UpdateProjectNetworksRequest, StateDb.Model.Project>
    {

        public Update([NotNull] ICreateEntityRequestHandler<StateDb.Model.Project> operationHandler) : base(operationHandler)
        {
        }

        protected override object CreateOperationMessage(UpdateProjectNetworksRequest request)
        {
            if (!request.Configuration.HasValue)
                return null;

            var configDictionary = ConfigModelJsonSerializer.DeserializeToDictionary(request.Configuration.Value);
            var config = ProjectNetworksConfigDictionaryConverter.Convert(configDictionary);

            return new CreateNetworksCommand{ 
                CorrelationId = request.CorrelationId == Guid.Empty 
                    ? new Guid()
                    : request.CorrelationId, 
                    Config = config };
        }

        [HttpPost("vnetworks")]
        [SwaggerOperation(
            Summary = "Creates or updates virtual networks of project",
            Description = "Creates or updates virtual networks",
            OperationId = "VNetworks_Create",
            Tags = new[] { "Virtual Networks" })
        ]

        public override Task<ActionResult<ListResponse<Operation>>> HandleAsync([FromBody] UpdateProjectNetworksRequest request, CancellationToken cancellationToken = default)
        {
            return base.HandleAsync(request, cancellationToken);
        }


    }
}
