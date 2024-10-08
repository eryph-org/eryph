using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

using static LanguageExt.Prelude;

namespace Eryph.Modules.AspNetCore.ApiProvider.Swagger;

public class SecurityRequirementsOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var scopes = context.ApiDescription.ActionDescriptor.EndpointMetadata
            .OfType<AuthorizeAttribute>()
            .Map(a => Optional(a.Policy))
            .Somes()
            .Distinct()
            .ToList();

        if (scopes.Count == 0)
            return;

        var oAuthScheme = new OpenApiSecurityScheme
        {
            Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "oauth2" }
        };

        operation.Security =
        [
            new OpenApiSecurityRequirement
            {
                [oAuthScheme] = scopes.ToList()
            }
        ];
    }
}
