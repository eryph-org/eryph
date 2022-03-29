using System;
using System.Text.Json.Serialization;
using Ardalis.Specification;
using Dbosoft.Hosuto.Modules;
using Eryph.Messages;
using Eryph.ModuleCore;
using Eryph.Modules.AspNetCore.ApiProvider;
using Eryph.Modules.AspNetCore.ApiProvider.Handlers;
using Eryph.Modules.AspNetCore.ApiProvider.Model.V1;
using Eryph.Rebus;
using Eryph.StateDb;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;
using Rebus.Handlers;
using Rebus.Retry.Simple;
using Rebus.Routing.TypeBased;
using Rebus.Serialization.Json;
using SimpleInjector;
using SimpleInjector.Integration.ServiceCollection;

namespace Eryph.Modules.AspNetCore
{
    public abstract class ApiModule<TModule> : WebModule where TModule : WebModule
    {
        public abstract string ApiName { get; }
        public abstract string AudienceName { get; }

        [UsedImplicitly]
        public void ConfigureServices(IServiceProvider serviceProvider, IServiceCollection services,
            IHostEnvironment env)
        {
            services.AddMvc(op => { }).AddApiProvider<TModule>(op => op.ApiName = ApiName)
                .AddJsonOptions(opts =>
                {
                    opts.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
                    opts.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
                });


            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.Authority = "https://localhost:62189/identity";
                    options.Audience = AudienceName;
                });

// build symbol to disable any authentication, will also require environment to be set to Development
#if DISABLE_AUTH
            if (env.IsDevelopment())
                // Disable authentication and authorization.
                services.TryAddSingleton<IPolicyEvaluator, DisableAuthenticationPolicyEvaluator>();
#endif

            services.AddDbContext<StateStoreContext>(options =>
                serviceProvider.GetRequiredService<IDbContextConfigurer<StateStoreContext>>().Configure(options));


            services.AddAutoMapper(typeof(TModule).Assembly, typeof(MapperProfile).Assembly);
        }

        public void Configure(IApplicationBuilder app)
        {
            app.UseApiProvider(this);
        }

        [UsedImplicitly]
        public void AddSimpleInjector(SimpleInjectorAddOptions options)
        {
            options.AddAspNetCore()
                .AddControllerActivation();
        }

        [UsedImplicitly]
        public virtual void ConfigureContainer(IServiceProvider serviceProvider, Container container)
        {
            container.Register(typeof(IReadonlyStateStoreRepository<>), typeof(ReadOnlyStateStoreRepository<>), Lifestyle.Scoped);
            container.Register(typeof(IStateStoreRepository<>), typeof(StateStoreRepository<>), Lifestyle.Scoped);
            container.Register(typeof(IListRequestHandler<>), typeof(ListRequestHandler<>),Lifestyle.Scoped);
            container.Register(typeof(IGetRequestHandler<>), typeof(GetRequestHandler<>), Lifestyle.Scoped);
            container.Register(typeof(IResourceOperationHandler<>), typeof(ResourceOperationHandler<>), Lifestyle.Scoped);
            container.Register(typeof(INewResourceOperationHandler<>), typeof(NewResourceOperationHandler<>), Lifestyle.Scoped);

            container.Register(typeof(IReadRepositoryBase<>), typeof(ReadOnlyStateStoreRepository<>), Lifestyle.Scoped);

            // ReSharper disable once PossiblyMistakenUseOfParamsMethod
            container.RegisterSingleton(typeof(ISingleResourceSpecBuilder<>), typeof(TModule).Assembly);
            container.RegisterSingleton(typeof(IListResourceSpecBuilder<>), typeof(TModule).Assembly);

            container.Collection.Register(typeof(IHandleMessages<>), typeof(TModule).Assembly);
            container.Register<IOperationDispatcher, OperationDispatcher>(Lifestyle.Scoped);

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
                        {TypeNameHandling = TypeNameHandling.None}))
                    .Logging(x => x.Trace()).Start();
            });
        }
    }
}