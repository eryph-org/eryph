using Haipa.Resources;
using Haipa.StateDb.Model;
using JetBrains.Annotations;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using Operation = Haipa.Modules.AspNetCore.ApiProvider.Model.V1.Operation;
using OperationLogEntry = Haipa.Modules.AspNetCore.ApiProvider.Model.V1.OperationLogEntry;
using OperationResource = Haipa.Modules.AspNetCore.ApiProvider.Model.V1.OperationResource;

namespace Haipa.Modules.AspNetCore.ApiProvider.Swagger
{
    [UsedImplicitly]
    public class OperationSchemaFilter : ISchemaFilter
    {
        public void Apply(OpenApiSchema schema, SchemaFilterContext context)
        {
            if (context.Type.FullName == typeof(Operation).FullName
                || context.Type.FullName == typeof(OperationLogEntry).FullName
                || context.Type.FullName == typeof(OperationStatus).FullName
                || context.Type.FullName == typeof(OperationResource).FullName
                || context.Type.FullName == typeof(ResourceType).FullName)
                schema.Extensions.Add("x-ms-external", new OpenApiBoolean(true));
        }
    }
}