using System.Collections.Generic;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Eryph.Modules.AspNetCore.ApiProvider.Swagger;

[UsedImplicitly]
public class ProblemDetailsOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        AddProblemDetails(operation, context);
    }

    private void AddProblemDetails(OpenApiOperation operation, OperationFilterContext context)
    {
        var errorSchema = context.EnsureSchemaPresentAndGetRef<ProblemDetails>();

        var response = new OpenApiResponse
        {
            Description = "Error response describing why the request failed",
            Content = new Dictionary<string, OpenApiMediaType>
            {
                {
                    "application/problem+json", new OpenApiMediaType
                    {
                        Schema = errorSchema
                    }
                }
            }
        };

        operation.Responses.Add("default", response);
    }
}
