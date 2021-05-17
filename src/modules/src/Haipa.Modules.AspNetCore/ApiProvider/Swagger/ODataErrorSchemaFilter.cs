using Haipa.Modules.AspNetCore.ApiProvider.Model;
using JetBrains.Annotations;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Haipa.Modules.AspNetCore.ApiProvider.Swagger
{
    [UsedImplicitly]
    public class ODataErrorSchemaFilter : ISchemaFilter
    {
        public void Apply(OpenApiSchema schema, SchemaFilterContext context)
        {
            if (context.Type == typeof(ApiError)
                || context.Type == typeof(ApiErrorData)
                || context.Type == typeof(ApiErrorBody)
                || context.Type == typeof(ApiErrorData.InnerErrorData))
                schema.Extensions.Add("x-ms-external", new OpenApiBoolean(true));
        }
    }
}