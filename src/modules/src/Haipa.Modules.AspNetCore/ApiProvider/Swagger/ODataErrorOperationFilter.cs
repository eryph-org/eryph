using System.Collections.Generic;
using Haipa.Modules.AspNetCore.ApiProvider.Model;
using JetBrains.Annotations;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Haipa.Modules.AspNetCore.ApiProvider.Swagger
{
    [UsedImplicitly]
    public class ODataErrorOperationFilter : IOperationFilter
    {
        public void Apply(OpenApiOperation operation, OperationFilterContext context)
        {
            var errorSchema = context.EnsureSchemaPresentAndGetRef<ApiError>();

            var response = new OpenApiResponse
            {
                Description = "Error response describing why the operation failed",
                Content = new Dictionary<string, OpenApiMediaType>
                {
                    {
                        "application/json", new OpenApiMediaType
                        {
                            Schema = errorSchema
                        }
                    }
                }
            };

            operation.Responses.Add("default", response);
        }
    }
}