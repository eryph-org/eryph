using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.Json;
using Asp.Versioning.ApiExplorer;
using Dbosoft.Hosuto.Modules;
using Eryph.Modules.AspNetCore.ApiProvider.Model;
using Eryph.Modules.AspNetCore.ApiProvider.Swagger;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Eryph.Modules.AspNetCore.ApiProvider
{
    public static class ApiProviderExtensions
    {
        public static IMvcBuilder AddApiProvider<TModule>(this IMvcBuilder mvcBuilder,
            Action<ApiProviderOptions> options) where TModule : WebModule
        {
            var services = mvcBuilder.Services;

            services.AddOptions<ApiProviderOptions>();
            services.Configure(options);

            services.AddTransient<ICorrelationIdGenerator, CorrelationIdGenerator>();
            //mvcBuilder.AddApplicationPart(typeof(VersionedMetadataController).Assembly);

            services.AddApiVersioning(options =>
            {
                options.ReportApiVersions = true;
                options.AssumeDefaultVersionWhenUnspecified = false;
            }).AddApiExplorer(options =>
            {
                // add the versioned api explorer, which also adds IApiVersionDescriptionProvider service
                // note: the specified format code will format the version as "'v'major[.minor][-status]"
                options.GroupNameFormat = "'v'VVV";
                // note: this option is only necessary when versioning by url segment. the SubstitutionFormat
                // can also be used to control the format of the API version in route templates
                options.SubstituteApiVersionInUrl = true;
            });

            services.Configure<MvcOptions>(op =>
            {
                //op.EnableEndpointRouting = false;
                //op.OutputFormatters.Insert(0, new CustomODataOutputFormatter());
            });

            services.AddTransient<IConfigureOptions<SwaggerGenOptions>, ConfigureSwaggerOptions>();
            services.AddSwaggerGen(
                options =>
                {
                    // add a custom operation filter which sets default values
                    options.OperationFilter<SwaggerDefaultValues>();
                    options.OperationFilter<ApiErrorOperationFilter>();
                    options.SchemaFilter<ApiErrorSchemaFilter>();
                    options.SchemaFilter<OperationSchemaFilter>();
                    options.OperationFilter<ListResponseOperationFilter>();


                    // integrate xml comments
                    var xmlFile = $"{typeof(TModule).Assembly.GetName().Name}.xml";

                    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);

                    if (File.Exists(xmlPath))
                        options.IncludeXmlComments(xmlPath);

                    //disable until openapi 3.0 is supported for autorest (including ruby)
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


                    options.ResolveConflictingActions(app => app.First());
                    options.EnableAnnotations();

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

        public static IApplicationBuilder UseApiProvider<TModule>(this IApplicationBuilder app, TModule module)
            where TModule : WebModule
        {
            //var modelBuilder = app.ApplicationServices.GetRequiredService<VersionedODataModelBuilder>();
            var provider = app.ApplicationServices.GetRequiredService<IApiVersionDescriptionProvider>();

            //var models = modelBuilder.GetEdmModels().ToArray();
            //routing is not supported currently
            //as versioned OData models are currently not supported with endpoint routing.
            //see also https://github.com/microsoft/aspnet-api-versioning/issues/647). 
            app.UseRouting();

            app.UseAuthentication();
            app.UseAuthorization();

            app.UseMiddleware<CorrelationIdMiddleware>();

            //uncomment this when endpoint routing is working again
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
                //endpoints.Select().Expand().Filter().OrderBy().MaxTop(100).Count();
                //endpoints.MapVersionedODataRoutes("odata-bypath", "odata/v{version:apiVersion}", models);
            });

            //app.UseMvc(b =>
            //{
            //    app.UseMvc(routes =>
            //    {
            //        routes.Select().Expand().Filter().OrderBy().MaxTop(100).Count().SkipToken();
            //        routes.MapVersionedODataRoutes("odata-bypath", "api/v{version:apiVersion}", models);
            //    });
            //});

            app.UseSwagger(c =>
            {
                c.SerializeAsV2 = false;
                c.PreSerializeFilters.Add((swaggerDoc, httpReq) =>
                {
                    swaggerDoc.Servers = new List<OpenApiServer>
                        {new OpenApiServer {Url = module.Path}};
                });
            });
            app.UseSwaggerUI(
                options =>
                {
                    options.DisplayOperationId();

                    // build a swagger endpoint for each discovered API version
                    foreach (var description in provider.ApiVersionDescriptions)
                        options.SwaggerEndpoint($"{module.Path}/swagger/{description.GroupName}/swagger.json",
                            description.GroupName.ToUpperInvariant());
                });


            app.UseExceptionHandler(appBuilder =>
            {
                appBuilder.Use(async (context, next) =>
                {
                    var env = appBuilder.ApplicationServices.GetRequiredService<IHostEnvironment>();
                    var error = context.Features[typeof(IExceptionHandlerFeature)] as IExceptionHandlerFeature;
                    if (error?.Error != null)
                    {
                        context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                        context.Response.ContentType = "application/json";
                        var apiError = ApiError.FromException(error.Error, env.IsDevelopment());
                       
                        await context.Response.WriteAsync(JsonSerializer.Serialize(apiError));
                    }

                    // when no error, do next.
                    else
                    {
                        await next();
                    }
                });
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
}