using Haipa.StateDb.Model;
using JetBrains.Annotations;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Haipa.Modules.ApiProvider.Swagger
{
    [UsedImplicitly]
    public class OperationSchemaFilter : ISchemaFilter
    {
        public void Apply(OpenApiSchema schema, SchemaFilterContext context)
        {
            if (context.Type.FullName == typeof(Operation).FullName
                || context.Type.FullName == typeof(OperationLogEntry).FullName
                || context.Type.FullName == typeof(OperationStatus).FullName
                || context.Type.FullName == typeof(OperationTask).FullName
                || context.Type.FullName == typeof(OperationTaskStatus).FullName
                || context.Type.FullName == typeof(OperationResource).FullName
                || context.Type.FullName == typeof(ResourceType).FullName)
                schema.Extensions.Add("x-ms-external", new OpenApiBoolean(true));
        }
    }
}