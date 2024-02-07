using System;
using System.Threading;
using System.Threading.Tasks;
using Eryph.ConfigModel.Catlets;
using Eryph.ConfigModel.Json;
using Eryph.Messages.Resources.Catlets.Commands;
using Eryph.Modules.AspNetCore;
using Eryph.Modules.AspNetCore.ApiProvider.Endpoints;
using Eryph.Modules.AspNetCore.ApiProvider.Handlers;
using Eryph.Modules.AspNetCore.ApiProvider.Model;
using Eryph.Modules.AspNetCore.ApiProvider.Model.V1;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using Eryph.StateDb.Specifications;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace Eryph.Modules.ComputeApi.Endpoints.V1.Catlets
{
    public class Create : NewOperationRequestEndpoint<NewCatletRequest, StateDb.Model.Catlet>
    {
        private readonly IReadonlyStateStoreRepository<StateDb.Model.Catlet> _repository;
        private readonly IUserRightsProvider _userRightsProvider;

        public Create([NotNull] ICreateEntityRequestHandler<StateDb.Model.Catlet> operationHandler, IReadonlyStateStoreRepository<Catlet> repository, IUserRightsProvider userRightsProvider) : base(operationHandler)
        {
            _repository = repository;
            _userRightsProvider = userRightsProvider;
        }

        protected override object CreateOperationMessage(NewCatletRequest request)
        {
            var jsonString = request.Configuration.GetValueOrDefault().ToString();

            var configDictionary = ConfigModelJsonSerializer.DeserializeToDictionary(jsonString);
            var config = CatletConfigDictionaryConverter.Convert(configDictionary);

            return new CreateCatletCommand{ 
                CorrelationId = request.CorrelationId == Guid.Empty 
                    ? new Guid()
                    : request.CorrelationId, 
                    Config = config };
        }

        [Authorize(Policy = "compute:catlets:write")]
        [HttpPost("catlets")]
        [SwaggerOperation(
            Summary = "Creates a new catlet",
            Description = "Creates a catlet",
            OperationId = "Catlets_Create",
            Tags = new[] { "Catlets" })
        ]

        //[SwaggerResponse(Microsoft.AspNetCore.Http.StatusCodes.Status202Accepted, "Success", typeof(Operation))]
        //[SwaggerResponse(Microsoft.AspNetCore.Http.StatusCodes.Status409Conflict, "Conflict")]

        public override async Task<ActionResult<ListResponse<Operation>>> HandleAsync([FromBody] NewCatletRequest request, CancellationToken cancellationToken = default)
        {
            var jsonString = request.Configuration.GetValueOrDefault().ToString();

            var configDictionary = ConfigModelJsonSerializer.DeserializeToDictionary(jsonString);
            var config = CatletConfigDictionaryConverter.Convert(configDictionary);

            var tenantId = _userRightsProvider.GetUserTenantId();
            
            var project = config.Project ?? "default";

            var projectAccess = await _userRightsProvider.HasProjectAccess(project, AccessRight.Write);
            if (!projectAccess)
                return Forbid();

            var existingCatlet = await _repository.GetBySpecAsync(new CatletSpecs.GetByName(config.Name ?? "catlet", tenantId,
                project), cancellationToken);


            if (existingCatlet != null)
                return Conflict();
            
            return await base.HandleAsync(request, cancellationToken);
        }


    }
}
