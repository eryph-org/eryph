﻿using System;
using System.Collections.Generic;
using System.Linq;
using Asp.Versioning.ApiExplorer;
using Eryph.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Eryph.Modules.AspNetCore.ApiProvider.Swagger
{
    /// <summary>
    ///     Configures the Swagger generation options.
    /// </summary>
    /// <remarks>
    ///     This allows API versioning to define a Swagger document per API version after the
    ///     <see cref="IApiVersionDescriptionProvider" /> service has been resolved from the service container.
    /// </remarks>
    public class ConfigureSwaggerOptions : IConfigureOptions<SwaggerGenOptions>
    {
        private readonly ApiProviderOptions _apiOptions;
        private readonly IApiVersionDescriptionProvider _provider;

        /// <summary>
        ///     Initializes a new instance of the <see cref="ConfigureSwaggerOptions" /> class.
        /// </summary>
        /// <param name="provider">
        ///     The <see cref="IApiVersionDescriptionProvider">provider</see> used to generate Swagger
        ///     documents.
        /// </param>
        /// <param name="apiOptions"></param>
        public ConfigureSwaggerOptions(IApiVersionDescriptionProvider provider, IOptions<ApiProviderOptions> apiOptions)
        {
            _provider = provider;
            _apiOptions = apiOptions.Value;
        }

        /// <inheritdoc />
        public void Configure(SwaggerGenOptions options)
        {
            // Add an OpenAPI document for each discovered API version.
            // Note: you might choose to skip or document deprecated API versions differently
            foreach (var description in _provider.ApiVersionDescriptions)
            {
                options.SwaggerDoc(description.GroupName, CreateInfoForApiVersion(description));
            }

            if (_apiOptions.OAuthOptions is not null)
            {
                options.AddSecurityDefinition(
                    EryphConstants.Authorization.SecuritySchemeId,
                    new OpenApiSecurityScheme
                    {
                        Type = SecuritySchemeType.OAuth2,
                        // Client assertions are not supported by the OpenAPI specification.
                        // See https://github.com/OAI/OpenAPI-Specification/issues/1875.
                        Description = """
                                      Eryph only supports the client credentials flow. Depending
                                      on the client, you can use either the client secret or a
                                      client assertion with type
                                      `urn:ietf:params:oauth:client-assertion-type:jwt-bearer`.
                                      """,
                        Flows = new OpenApiOAuthFlows()
                        {
                            ClientCredentials = new OpenApiOAuthFlow()
                            {
                                TokenUrl = _apiOptions.OAuthOptions.TokenEndpoint,
                                Scopes = _apiOptions.OAuthOptions.Scopes
                                    .ToDictionary(s => s.Name, s => s.Description),
                            },
                        },
                    });
            }
        }

        private OpenApiInfo CreateInfoForApiVersion(ApiVersionDescription description)
        {
            var info = new OpenApiInfo
            {
                Title = _apiOptions.ApiName,
                Version = description.ApiVersion.ToString(),
                Description = _apiOptions.ApiName,
                Contact = new OpenApiContact
                {
                    Name = "dbosoft",
                    Email = "support@dbosoft.eu",
                },
                License = new OpenApiLicense
                {
                    Name = "MIT",
#pragma warning disable S1075
                    Url = new Uri("https://opensource.org/licenses/MIT"),
#pragma warning restore S1075
                },
            };

            if (description.IsDeprecated) info.Description += " This API version has been deprecated.";

            return info;
        }
    }
}