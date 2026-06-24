using System;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Eryph.Modules.AspNetCore.ApiProvider.Swagger;

public static class OperationFilterContextExtensions
{
    /// <param name="self">The operation filter</param>
    extension(OperationFilterContext self)
    {
        /// <summary>
        ///     Makes sure that schema repository contains model for <typeparamref name="TModel" />.
        ///     Returns OpenAPI schema as a reference to the schema in repository.
        /// </summary>
        /// <typeparam name="TModel">Model type to be represented in schema repository</typeparam>
        /// <returns>The OpenAPI schema for <typeparamref name="TModel" /></returns>
        public OpenApiSchema EnsureSchemaPresentAndGetRef<TModel>()
        {
            return self.EnsureSchemaPresentAndGetRef(typeof(TModel));
        }

        public OpenApiSchema EnsureSchemaPresentAndGetRef(Type type)
        {
            if (!self.SchemaRepository.Schemas.TryGetValue(type.Name, out _))
            {
                var refSchema = self.SchemaGenerator.GenerateSchema(type, self.SchemaRepository);

                return refSchema;
            }

            var createdRefSchema = new OpenApiSchema
            {
                Reference = new OpenApiReference
                {
                    Id = type.Name,
                    Type = ReferenceType.Schema,
                },
            };

            return createdRefSchema;
        }
    }
}
