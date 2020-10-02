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
            if (context.Type == typeof(Operation)
                || context.Type == typeof(OperationLogEntry)
                || context.Type == typeof(OperationStatus)
                || context.Type == typeof(OperationTask)
                || context.Type == typeof(OperationTaskStatus)
                || context.Type == typeof(OperationResource)
                || context.Type == typeof(ResourceType))
                schema.Extensions.Add("x-ms-external", new OpenApiBoolean(true));
        }
    }
}