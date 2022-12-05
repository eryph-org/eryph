using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Eryph.ConfigModel.Catlets;
using Eryph.ConfigModel.Json;
using Eryph.Messages.Resources.Catlets.Commands;
using Eryph.Modules.AspNetCore.ApiProvider.Endpoints;
using Eryph.Modules.AspNetCore.ApiProvider.Handlers;
using Eryph.Modules.AspNetCore.ApiProvider.Model;
using Eryph.Modules.AspNetCore.ApiProvider.Model.V1;
using Eryph.Resources;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Swashbuckle.AspNetCore.Annotations;

namespace Eryph.Modules.ComputeApi.Endpoints.V1.Catlets
{
    public class Update : ResourceOperationEndpoint<UpdateCatletRequest, StateDb.Model.Catlet>
    {
        
        public Update([NotNull] IResourceOperationHandler<StateDb.Model.Catlet> operationHandler) : base(operationHandler)
        {
        }

        protected override object CreateOperationMessage(StateDb.Model.Catlet model, UpdateCatletRequest request )
        {

            var jsonString = request.Configuration.GetValueOrDefault().ToString();

            var configDictionary = ConfigModelJsonSerializer.DeserializeToDictionary(jsonString);
            var config = CatletConfigDictionaryConverter.Convert(configDictionary);

            return new UpdateCatletCommand(){Resource = new Resource(ResourceType.Machine, model.Id), 
                CorrelationId = request.CorrelationId, Config = config};
        }


        [HttpPut("catlet")]
        [SwaggerOperation(
            Summary = "Updates a catlet",
            Description = "Updates a catlet",
            OperationId = "Catlets_Update",
            Tags = new[] { "Catlets" })
        ]

        public override Task<ActionResult<ListResponse<Operation>>> HandleAsync([FromBody] UpdateCatletRequest request, CancellationToken cancellationToken = default)
        {
            return base.HandleAsync(request, cancellationToken);
        }


    }
}
