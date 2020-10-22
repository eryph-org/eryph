using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using JetBrains.Annotations;
using Microsoft.AspNet.OData;
using Microsoft.AspNet.OData.Query;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Interfaces;
using Microsoft.OpenApi.Models;
using SimpleInjector;
using Swashbuckle.AspNetCore.Annotations;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Haipa.Modules.ApiProvider.Swagger
{
    [UsedImplicitly]
    public class ODataQueryOperationFilter : IOperationFilter
    {
        public void Apply(OpenApiOperation operation, OperationFilterContext context)
        {
            var queryAttribute = context.MethodInfo.GetCustomAttribute<EnableQueryAttribute>();
            var responseType = context.MethodInfo.GetCustomAttribute<SwaggerResponseAttribute>();

            var hasQueryOptions = context.MethodInfo.GetParameters()
                .Any(x => x.ParameterType.IsClosedTypeOf(typeof(ODataQueryOptions<>)));

            if ((queryAttribute == null && !hasQueryOptions) || responseType == null)
                return;

            if (operation.Parameters.All(x => x.Name != "$filter"))
                return;

            var referenceType = responseType.Type;
            if (responseType.Type.IsClosedTypeOf(typeof(ODataValueEx<>)))
            {
                // get inner list type for odata reference
                referenceType = referenceType.GetGenericArguments().First().GetGenericArguments().First();
            }
            var reference = context.EnsureSchemaPresentAndGetRef(referenceType);

            operation.Extensions.Add(new KeyValuePair<string, IOpenApiExtension>("x-ms-odata", new OpenApiString(reference.Reference.ReferenceV2)));
            operation.Extensions.Add(new KeyValuePair<string, IOpenApiExtension>("x-ms-pageable", new OpenApiObject
            {
                { "nextLinkName",new OpenApiString("@odata.nextLink")}
            }));

        }

    }
}