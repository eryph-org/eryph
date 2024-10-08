using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Asp.Versioning.ApiExplorer;
using Dbosoft.Hosuto.Modules;
using Eryph.Modules.AspNetCore.ApiProvider.Swagger;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Eryph.Modules.AspNetCore.ApiProvider;

public static class ApiProviderExtensions
{
    public static IMvcBuilder AddApiProvider<TModule>(
        this IMvcBuilder mvcBuilder,
        Action<ApiProviderOptions> apiOptions)
        where TModule : WebModule
    {
        var services = mvcBuilder.Services;

        services.AddOptions<ApiProviderOptions>();
        services.Configure(apiOptions);

        mvcBuilder.AddMvcOptions(options =>
        {
            // Remove media types which are not required as we only accept JSON requests.
            // This also prevents unwanted media types (e.g. text/plain) from showing
            // up in the OpenAPI specifications.
            var jsonInputFormatter = options.InputFormatters.OfType<SystemTextJsonInputFormatter>().Single();
            jsonInputFormatter.SupportedMediaTypes.Clear();
            jsonInputFormatter.SupportedMediaTypes.Add("application/json");
        });
        
        mvcBuilder.AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.AddEryphApiSettings();
        });

        services.AddProblemDetails(problemDetailsOptions =>
        {
            problemDetailsOptions.CustomizeProblemDetails = context =>
            {
                context.ProblemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
            };
        });

        services.AddApiVersioning(options =>
            {
                options.ReportApiVersions = true;
                options.AssumeDefaultVersionWhenUnspecified = false;
            })
            .AddApiExplorer(options =>
            {
                // Add the versioned api explorer, which also adds IApiVersionDescriptionProvider service.
                // Note: the specified format code will format the version as "'v'major[.minor][-status]"
                options.GroupNameFormat = "'v'VVV";
                // Note: this option is only necessary when versioning by url segment. The SubstitutionFormat
                // can also be used to control the format of the API version in route templates
                options.SubstituteApiVersionInUrl = true;
            });

        services.AddTransient<IConfigureOptions<SwaggerGenOptions>, ConfigureSwaggerOptions>();
        services.AddSwaggerGen(options =>
        {
            options.OperationFilter<SwaggerDefaultValues>();
            options.OperationFilter<ProblemDetailsOperationFilter>();
            options.OperationFilter<ListResponseOperationFilter>();
            options.OperationFilter<SecurityRequirementsOperationFilter>();

            // Integrate xml comments
            var xmlFile = $"{typeof(TModule).Assembly.GetName().Name}.xml";
            var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
            if (File.Exists(xmlPath))
                options.IncludeXmlComments(xmlPath);


            
            //options.AddSecurityDefinition("bearer", new OpenApiSecurityScheme
            //{
            //    Description = "JWT Authorization header using the Bearer scheme (Example: 'Bearer 12345abcdef')",
            //    Name = "Authorization",
            //    In = ParameterLocation.Header,
            //    Type = SecuritySchemeType.Http,
            //    Scheme = "bearer",
            //});

            //options.AddSecurityRequirement(new OpenApiSecurityRequirement
            //{
            //    {
            //        new OpenApiSecurityScheme
            //        {
            //            Reference = new OpenApiReference
            //            {
            //                Type = ReferenceType.SecurityScheme,
            //                Id = "bearer"
            //            }
            //        },
            //        Array.Empty<string>()
            //    }
            //});

            options.EnableAnnotations();
            options.SupportNonNullableReferenceTypes();

            options.CustomSchemaIds(type =>
            {
                var defaultName = DefaultSchemaIdSelector(type);
                return defaultName.EndsWith("ListResponse")
                    ? defaultName.Replace("ListResponse", "List")
                    : defaultName;
            });
        });

        services.AddSingleton<IAuthorizationHandler, HasScopeHandler>();

        return mvcBuilder;
    }

    public static IApplicationBuilder UseApiProvider<TModule>(
        this IApplicationBuilder app,
        TModule module)
        where TModule : WebModule
    {
        var provider = app.ApplicationServices.GetRequiredService<IApiVersionDescriptionProvider>();

        app.UseMiddleware<RequestIdMiddleware>();

        app.UseExceptionHandler();
        app.UseStatusCodePages();

        app.UseRouting();

        app.UseAuthentication();
        app.UseAuthorization();

        app.UseEndpoints(endpoints =>
        {
            endpoints.MapControllers();
        });

        app.UseSwagger(c =>
        {
            c.PreSerializeFilters.Add((swaggerDoc, _) =>
            {
                swaggerDoc.Servers =
                [
                    new OpenApiServer { Url = module.Path },
                ];
            });
        });
        
        app.UseSwaggerUI(options =>
        {
            options.DisplayOperationId();

            // Build a swagger endpoint for each discovered API version
            foreach (var description in provider.ApiVersionDescriptions)
            {
                options.SwaggerEndpoint($"{module.Path}/swagger/{description.GroupName}/swagger.json",
                    description.GroupName.ToUpperInvariant());
            }
        });

        return app;
    }

    private static string DefaultSchemaIdSelector(Type modelType)
    {
        if (!modelType.IsConstructedGenericType) return modelType.Name;

        var prefix = modelType.GetGenericArguments()
            .Select(DefaultSchemaIdSelector)
            .Aggregate((previous, current) => previous + current);

        return prefix + modelType.Name.Split('`').First();
    }
}
