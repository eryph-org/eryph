using System;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Haipa.Modules.ApiProvider.Swagger
{
    public static class OperationFilterContextExtensions
    {

        /// <summary>
        /// Makes sure that schema repository contains model for <typeparamref name="TModel"/>.
        /// Returns OpenAPI schema as a reference to the schema in repository.
        /// </summary>
        /// <typeparam name="TModel">Model type to be represented in schema repository</typeparam>
        /// <param name="self">The operation filter</param>
        /// <returns>The OpenAPI schema for <typeparamref name="TModel"/></returns>
        public static OpenApiSchema EnsureSchemaPresentAndGetRef<TModel>(
            this OperationFilterContext self) => self.EnsureSchemaPresentAndGetRef(typeof(TModel));


        public static OpenApiSchema EnsureSchemaPresentAndGetRef(
            this OperationFilterContext self, Type type)
        {
            if (!self.SchemaRepository.Schemas.TryGetValue(type.Name, out _))
            {
                var refSchema = self.SchemaGenerator.GenerateSchema(type, self.SchemaRepository);
                
                return refSchema;
            }

            var createdRefSchema = new OpenApiSchema()
            {
                Reference = new OpenApiReference()
                {
                    Id = type.Name,
                    Type = ReferenceType.Schema
                }
            };

            return createdRefSchema;
        }
    }
}