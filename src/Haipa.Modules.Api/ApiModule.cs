using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using Dbosoft.Hosuto.Modules;
using Haipa.Messages;
using Haipa.Modules.Api.Services;
using Haipa.Rebus;
using Haipa.StateDb;
using JetBrains.Annotations;
using Microsoft.AspNet.OData;
using Microsoft.AspNet.OData.Builder;
using Microsoft.AspNet.OData.Extensions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Rebus.Handlers;
using Rebus.Logging;
using Rebus.Retry.Simple;
using Rebus.Routing.TypeBased;
using Rebus.Serialization.Json;
using SimpleInjector;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Haipa.Modules.Api
{
    [UsedImplicitly]
    public class ApiModule : WebModule
    {
        public override string Name => "Haipa.Modules.Api";
        public override string Path => "api";

        public void ConfigureServices(IServiceProvider serviceProvider, IServiceCollection services)
        {
            services.AddDbContext<StateStoreContext>(options =>
                serviceProvider.GetRequiredService<IDbContextConfigurer<StateStoreContext>>().Configure(options));

            services.AddMvc(op =>
                {
                    op.EnableEndpointRouting = false;
                    var policy = new AuthorizationPolicyBuilder()
                        .RequireAuthenticatedUser()
                        .Build();
                    op.Filters.Add(new AuthorizeFilter(policy));
                })
                .SetCompatibilityVersion(CompatibilityVersion.Version_2_2)
                .AddApplicationPart(typeof(ApiModule).Assembly)
                .AddApplicationPart(typeof(VersionedMetadataController).Assembly);


            JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();
            JwtSecurityTokenHandler.DefaultOutboundClaimTypeMap.Clear();

            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.Authority = "https://localhost:62189/identity";
                    options.Audience = "compute_api";
                    options.RequireHttpsMetadata = false;
                });


            services.AddApiVersioning(options =>
            {
                options.ReportApiVersions = true;
                options.AssumeDefaultVersionWhenUnspecified = false;
            });
            services.AddOData().EnableApiVersioning();

            services.AddODataApiExplorer(
                options =>
                {
                    // add the versioned api explorer, which also adds IApiVersionDescriptionProvider service
                    // note: the specified format code will format the version as "'v'major[.minor][-status]"
                    options.GroupNameFormat = "'v'VVV";

                    // note: this option is only necessary when versioning by url segment. the SubstitutionFormat
                    // can also be used to control the format of the API version in route templates
                    options.SubstituteApiVersionInUrl = true;


                });
            services.AddTransient<IConfigureOptions<SwaggerGenOptions>, ConfigureSwaggerOptions>();
            services.AddSwaggerGen(
                options =>
                {
                    // add a custom operation filter which sets default values
                    options.OperationFilter<SwaggerDefaultValues>();

                    //// integrate xml comments
                    //options.IncludeXmlComments(XmlCommentsFilePath);

                    options.ResolveConflictingActions(app => app.First());
                    options.EnableAnnotations();
                    options.DescribeAllEnumsAsStrings();
                });
        }

        public void Configure(IApplicationBuilder app)
        {
            var modelBuilder = app.ApplicationServices.GetService<VersionedODataModelBuilder>();
            var provider = app.ApplicationServices.GetService<IApiVersionDescriptionProvider>();

            app.UseAuthentication();

            app.UseMvc(b =>

            {
                b.Select().Expand().Filter().OrderBy().MaxTop(100).Count();
                var models = modelBuilder.GetEdmModels().ToArray();
                app.UseMvc(routes =>
                {
                    //routes.MapVersionedODataRoutes("odata", "odata", models);
                    routes.MapVersionedODataRoutes("odata-bypath", "odata/v{version:apiVersion}", models);
                });

            });

            app.UseSwagger();
            app.UseSwaggerUI(
                options =>
                {
                    options.DisplayOperationId();

                    // build a swagger endpoint for each discovered API version
                    foreach (var description in provider.ApiVersionDescriptions)
                    {
                        options.SwaggerEndpoint($"/api/swagger/{description.GroupName}/swagger.json", description.GroupName.ToUpperInvariant());
                        
                    }
                });
        }

        public void ConfigureContainer(IServiceProvider serviceProvider, Container container)
        {
            container.Collection.Register(typeof(IHandleMessages<>), typeof(ApiModule).Assembly);
            container.Register<IOperationManager, OperationManager>(Lifestyle.Scoped);

            container.ConfigureRebus(configurer =>
            {
                return configurer
                    .Transport(t =>
                        serviceProvider.GetRequiredService<IRebusTransportConfigurer>().ConfigureAsOneWayClient(t))
                    .Routing(x => x.TypeBased()
                        .Map(MessageTypes.ByRecipient(MessageRecipient.Controllers), QueueNames.Controllers))                        
                    .Options(x =>
                    {
                        x.SimpleRetryStrategy();
                        x.SetNumberOfWorkers(5);
                    })
                    .Serialization(x => x.UseNewtonsoftJson(new JsonSerializerSettings
                        { TypeNameHandling = TypeNameHandling.None }))
                    .Logging(x => x.ColoredConsole(LogLevel.Debug)).Start();
            });
        }
    }
}
