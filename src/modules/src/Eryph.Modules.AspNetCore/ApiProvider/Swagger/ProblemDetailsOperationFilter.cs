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
        AddValidationProblemDetails(operation, context);
        AddProblemDetails(operation, context);
    }

    private void AddProblemDetails(OpenApiOperation operation, OperationFilterContext context)
    {
        var errorSchema = context.EnsureSchemaPresentAndGetRef<ProblemDetails>();

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
    private void AddValidationProblemDetails(OpenApiOperation operation, OperationFilterContext context)
    {
        var errorSchema = context.EnsureSchemaPresentAndGetRef<ValidationProblemDetails>();

        var response = new OpenApiResponse
        {
            Description = "Error response describing why the request is invalid",
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

        operation.Responses.Add("400", response);
    }
}
