using Eryph.Resources;
using Eryph.StateDb.Model;
using JetBrains.Annotations;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using Operation = Eryph.Modules.AspNetCore.ApiProvider.Model.V1.Operation;
using OperationLogEntry = Eryph.Modules.AspNetCore.ApiProvider.Model.V1.OperationLogEntry;
using OperationResource = Eryph.Modules.AspNetCore.ApiProvider.Model.V1.OperationResource;
using Project = Eryph.Modules.AspNetCore.ApiProvider.Model.V1.Project;

namespace Eryph.Modules.AspNetCore.ApiProvider.Swagger
{
    [UsedImplicitly]
    public class OperationSchemaFilter : ISchemaFilter
    {
        public void Apply(OpenApiSchema schema, SchemaFilterContext context)
        {
            if (context.Type.FullName == typeof(Operation).FullName
                || context.Type.FullName == typeof(OperationLogEntry).FullName
                || context.Type.FullName == typeof(OperationStatus).FullName
                || context.Type.FullName == typeof(OperationResource).FullName
                || context.Type.FullName == typeof(Project).FullName
                || context.Type.FullName == typeof(ResourceType).FullName)
                schema.Extensions.Add("x-ms-external", new OpenApiBoolean(true));
        }
    }
}