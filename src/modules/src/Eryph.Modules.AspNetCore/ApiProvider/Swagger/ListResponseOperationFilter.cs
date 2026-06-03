using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Eryph.Modules.AspNetCore.ApiProvider.Model;
using JetBrains.Annotations;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using SimpleInjector;
using Swashbuckle.AspNetCore.Annotations;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Eryph.Modules.AspNetCore.ApiProvider.Swagger;

[UsedImplicitly]
public class ListResponseOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        // An action may declare several [SwaggerResponse] attributes (e.g. 200/400/401), so enumerate
        // them — GetCustomAttribute (singular) throws AmbiguousMatchException when more than one is
        // present. We only care about the one carrying a paged ListResponse<> body.
        var isPageable = context.MethodInfo
            .GetCustomAttributes<SwaggerResponseAttribute>(true)
            .Any(attr => attr.Type is not null && attr.Type.IsClosedTypeOf(typeof(ListResponse<>)));

        if (!isPageable)
            return;

        operation.Extensions.Add("x-ms-pageable", new OpenApiObject
        {
            // The explicit null value tells autorest that pagination is not supported.
            // The generated clients will still return the included list directly.
            ["nextLinkName"] = new OpenApiNull(),
        });
    }
}
