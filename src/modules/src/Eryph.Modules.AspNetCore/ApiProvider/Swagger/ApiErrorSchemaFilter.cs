using JetBrains.Annotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Eryph.Modules.AspNetCore.ApiProvider.Swagger
{
    [UsedImplicitly]
    public class ApiErrorSchemaFilter : ISchemaFilter
    {
        public void Apply(OpenApiSchema schema, SchemaFilterContext context)
        {
            if (context.Type == typeof(ProblemDetails)
                || context.Type == typeof(ValidationProblemDetails))
                schema.Extensions.Add("x-ms-external", new OpenApiBoolean(true));
        }
    }
}