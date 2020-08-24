using System;
using Dbosoft.Hosuto.Modules;
using Haipa.Messages;
using Haipa.Modules.Api.Services;
using Haipa.Modules.ApiProvider;
using Haipa.Rebus;
using Haipa.StateDb;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Rebus.Handlers;
using Rebus.Retry.Simple;
using Rebus.Routing.TypeBased;
using Rebus.Serialization.Json;
using SimpleInjector;

namespace Haipa.Modules.Api
{
    [UsedImplicitly]
    public class ApiModule : WebModule
    {
        public override string Name => "Haipa.Modules.Api";
        public override string Path => "api";

        [UsedImplicitly]
        public void ConfigureServices(IServiceProvider serviceProvider, IServiceCollection services)
        {

            services.AddMvc(op =>
            {
                var policy = new AuthorizationPolicyBuilder()
                    .RequireAuthenticatedUser()
                    .Build();
                op.Filters.Add(new AuthorizeFilter(policy));

            }).AddApiProvider<ApiModule>(op => op.ApiName = "Haipa Compute Api");

            
            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.Authority = "https://localhost:62189/identity";
                    options.Audience = "compute_api";
                });
            

            services.AddDbContext<StateStoreContext>(options =>
                serviceProvider.GetRequiredService<IDbContextConfigurer<StateStoreContext>>().Configure(options));

        }

        public void Configure(IApplicationBuilder app)
        {
            app.UseApiProvider(this);
        }

        [UsedImplicitly]
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
                    .Logging(x => x.ColoredConsole()).Start();
            });
        }
    }


}
