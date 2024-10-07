using System.Collections.Generic;
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
        var responseType = context.MethodInfo.GetCustomAttribute<SwaggerResponseAttribute>(true);

        if (responseType is null)
            return;

        if (!responseType.Type.IsClosedTypeOf(typeof(ListResponse<>)))
            return;

        operation.Extensions.Add("x-ms-pageable", new OpenApiObject
        {
            // The explicit null value tells autorest that pagination is not supported.
            // The generated clients will still return the included list directly.
            ["nextLinkName"] = new OpenApiNull(),
        });
    }
}
