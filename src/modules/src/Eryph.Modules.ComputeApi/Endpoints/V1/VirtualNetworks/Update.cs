using System;
using System.Threading;
using System.Threading.Tasks;
using Eryph.ConfigModel.Json;
using Eryph.ConfigModel.Networks;
using Eryph.Messages.Resources.Networks.Commands;
using Eryph.Modules.AspNetCore;
using Eryph.Modules.AspNetCore.ApiProvider.Endpoints;
using Eryph.Modules.AspNetCore.ApiProvider.Handlers;
using Eryph.Modules.AspNetCore.ApiProvider.Model;
using Eryph.Modules.AspNetCore.ApiProvider.Model.V1;
using Eryph.StateDb.Model;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace Eryph.Modules.ComputeApi.Endpoints.V1.VirtualNetworks
{
    public class Update : NewOperationRequestEndpoint<UpdateProjectNetworksRequest, StateDb.Model.Project>
    {
        private readonly IUserRightsProvider _userRightsProvider;
        public Update([NotNull] ICreateEntityRequestHandler<StateDb.Model.Project> operationHandler, IUserRightsProvider userRightsProvider) : base(operationHandler)
        {
            _userRightsProvider = userRightsProvider;
        }

        protected override object CreateOperationMessage(UpdateProjectNetworksRequest request)
        {
            if (!request.Configuration.HasValue)
                return null;

            var configDictionary = ConfigModelJsonSerializer.DeserializeToDictionary(request.Configuration.Value);
            if (configDictionary == null)
                return null;

            var config = ProjectNetworksConfigDictionaryConverter.Convert(configDictionary);

            return new CreateNetworksCommand{ 
                CorrelationId = request.CorrelationId == Guid.Empty 
                    ? new Guid()
                    : request.CorrelationId, 
                    Config = config,
                    TenantId = _userRightsProvider.GetUserTenantId()
            };
        }

        [Authorize(Policy = "compute:projects:write")]
        // ReSharper disable once StringLiteralTypo
        [HttpPost("vnetworks")]
        [SwaggerOperation(
            Summary = "Creates or updates virtual networks of project",
            Description = "Creates or updates virtual networks",
            OperationId = "VNetworks_Create",
            Tags = new[] { "Virtual Networks" })
        ]

        public override async Task<ActionResult<ListResponse<Operation>>> HandleAsync([FromBody] UpdateProjectNetworksRequest request, CancellationToken cancellationToken = default)
        {
            ProjectNetworksConfig config = null;
            if (request.Configuration != null)
            {
                var configDictionary = ConfigModelJsonSerializer.DeserializeToDictionary(request.Configuration.Value);
                if (configDictionary != null)
                {
                    config = ProjectNetworksConfigDictionaryConverter.Convert(configDictionary);
                }
            }

            if (config == null)
                return BadRequest();

            var projectAccess = await _userRightsProvider.HasProjectAccess(config.Project ?? "default", AccessRight.Admin);
            if (!projectAccess)
                return Forbid();

            return await base.HandleAsync(request, cancellationToken);
        }


    }
}
