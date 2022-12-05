using System.Collections.Generic;
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
using JetBrains.Annotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Swashbuckle.AspNetCore.Annotations;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Eryph.Modules.ComputeApi.Endpoints.V1.Catlets
{
    public class Create : NewResourceOperationEndpoint<NewCatletRequest, StateDb.Model.Catlet>
    {

        public Create([NotNull] INewResourceOperationHandler<StateDb.Model.Catlet> operationHandler) : base(operationHandler)
        {
        }

        protected override object CreateOperationMessage(NewCatletRequest request)
        {
            var jsonString = request.Configuration.GetValueOrDefault().ToString();

            var configDictionary = ConfigModelJsonSerializer.DeserializeToDictionary(jsonString);
            var config = CatletConfigDictionaryConverter.Convert(configDictionary);

            return new CreateCatletCommand{ CorrelationId = request.CorrelationId, Config = config };
        }
        
        [HttpPost("catlets")]
        [SwaggerOperation(
            Summary = "Creates a new catlet",
            Description = "Creates a catlet",
            OperationId = "Catlets_Create",
            Tags = new[] { "Catlets" })
        ]

        public override Task<ActionResult<ListResponse<Operation>>> HandleAsync([FromBody] NewCatletRequest request, CancellationToken cancellationToken = default)
        {
            return base.HandleAsync(request, cancellationToken);
        }


    }
}
