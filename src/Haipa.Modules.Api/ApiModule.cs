using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
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
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Rebus.Handlers;
using Rebus.Logging;
using Rebus.Retry.Simple;
using Rebus.Routing.TypeBased;
using Rebus.Serialization.Json;
using SimpleInjector;

namespace Haipa.Modules.Api
{
    [UsedImplicitly]
    public class ApiModule : WebModuleBase
    {
        public override string Name => "Haipa.Api";
        public override string Path => "api";


        protected override void ConfigureServices(IServiceProvider serviceProvider, IServiceCollection services)
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
                    options.Authority = "http://localhost:62189/identity";
                    options.Audience = "resource_server";
                    options.RequireHttpsMetadata = false;
                });


            services.AddApiVersioning(options =>
            {
                options.ReportApiVersions = true;
                options.AssumeDefaultVersionWhenUnspecified = true;
            });
            services.AddOData().EnableApiVersioning();
        }

        protected override void Configure(IApplicationBuilder app)
        {
            var modelBuilder = app.ApplicationServices.GetService<VersionedODataModelBuilder>();


            app.UseAuthentication();

            app.UseMvc(b =>

            {
                b.Select().Expand().Filter().OrderBy().MaxTop(100).Count();
                var models = modelBuilder.GetEdmModels().ToArray();
                app.UseMvc(routes =>
                {
                    routes.MapVersionedODataRoutes("odata", "odata", models);
                    routes.MapVersionedODataRoutes("odata-bypath", "odata/v{version:apiVersion}", models);
                });

            });
        }

        protected override void ConfigureContainer(IServiceProvider serviceProvider, Container container)
        {
            container.Collection.Register(typeof(IHandleMessages<>), typeof(ApiModule).Assembly);
            container.Register<IOperationManager, OperationManager>(Lifestyle.Scoped);

            container.ConfigureRebus(configurer =>
            {
                return configurer
                    .Transport(t =>
                        serviceProvider.GetRequiredService<IRebusTransportConfigurer>().ConfigureAsOneWayClient(t))
                    .Routing(x => x.TypeBased()
                        .MapAssemblyOf<ConvergeVirtualMachineCommand>("haipa.controller"))
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
