using System;
using System.Threading;
using System.Threading.Tasks;
using Eryph.ConfigModel.Catlets;
using Eryph.ConfigModel.Json;
using Eryph.Messages.Resources.Catlets.Commands;
using Eryph.Modules.AspNetCore.ApiProvider;
using Eryph.Modules.AspNetCore.ApiProvider.Endpoints;
using Eryph.Modules.AspNetCore.ApiProvider.Handlers;
using Eryph.Modules.AspNetCore.ApiProvider.Model;
using Eryph.Resources;
using Eryph.StateDb.Model;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using Operation = Eryph.Modules.AspNetCore.ApiProvider.Model.V1.Operation;
using Resource = Eryph.Resources.Resource;

namespace Eryph.Modules.ComputeApi.Endpoints.V1.Catlets
{
    public class Update : ResourceOperationEndpoint<UpdateCatletRequest, Catlet>
    {

        public Update([NotNull] IOperationRequestHandler<Catlet> operationHandler, 
            [NotNull] ISingleEntitySpecBuilder<SingleEntityRequest, Catlet> specBuilder) : base(operationHandler, specBuilder)
        {
        }

        protected override object CreateOperationMessage(Catlet model, UpdateCatletRequest request )
        {

            var jsonString = request.Configuration.GetValueOrDefault().ToString();

            var configDictionary = ConfigModelJsonSerializer.DeserializeToDictionary(jsonString);
            var config = CatletConfigDictionaryConverter.Convert(configDictionary);

            return new UpdateCatletCommand{Resource = new Resource(ResourceType.Catlet, model.Id),
                CorrelationId = request.CorrelationId == Guid.Empty
                    ? new Guid()
                    : request.CorrelationId,
                Config = config
            };
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
