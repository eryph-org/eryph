﻿using System;
using System.Text.Json.Serialization;
using Ardalis.Specification;
using Dbosoft.Hosuto.Modules;
using Dbosoft.Rebus;
using Dbosoft.Rebus.Configuration;
using Dbosoft.Rebus.Operations;
using Dbosoft.Rebus.Operations.Workflow;
using Eryph.ModuleCore;
using Eryph.Modules.AspNetCore.ApiProvider;
using Eryph.Modules.AspNetCore.ApiProvider.Handlers;
using Eryph.Modules.AspNetCore.ApiProvider.Model.V1;
using Eryph.Rebus;
using Eryph.StateDb;
using Eryph.StateDb.Workflows;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Rebus.Config;
using Rebus.Handlers;
using Rebus.Retry.Simple;
using SimpleInjector;
using SimpleInjector.Integration.ServiceCollection;

namespace Eryph.Modules.AspNetCore
{
    public abstract class ApiModule<TModule> : WebModule where TModule : WebModule
    {
        public abstract string ApiName { get; }
        public abstract string AudienceName { get; }

        [UsedImplicitly]
        public virtual void ConfigureServices(IServiceProvider serviceProvider, IServiceCollection services,
            IHostEnvironment env)
        {
            var endpointResolver = serviceProvider.GetRequiredService<IEndpointResolver>();
            services.AddSingleton(endpointResolver);

            services.AddMvc(op => { }).AddApiProvider<TModule>(op => op.ApiName = ApiName)
                .AddJsonOptions(opts =>
                {
                    opts.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
                    opts.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
                });


            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.Authority = endpointResolver.GetEndpoint("identity").ToString();

                    options.Audience = AudienceName;
                    if (env.IsDevelopment())
                        options.RequireHttpsMetadata = false;
                });

// build symbol to disable any authentication, will also require environment to be set to Development
#if DISABLE_AUTH
            if (env.IsDevelopment())
                // Disable authentication and authorization.
                services.TryAddSingleton<IPolicyEvaluator, DisableAuthenticationPolicyEvaluator>();
#endif

            services.AddAutoMapper(typeof(TModule).Assembly, typeof(MapperProfile).Assembly);
        }

        public void Configure(IApplicationBuilder app)
        {
            app.UseApiProvider(this);
        }

        [UsedImplicitly]
        public void AddSimpleInjector(SimpleInjectorAddOptions options)
        {
            options.AddAspNetCore().AddControllerActivation();
            options.AddLogging();
        }

        [UsedImplicitly]
        public virtual void ConfigureContainer(IServiceProvider serviceProvider, Container container)
        {
            container.Register(
                serviceProvider.GetRequiredService<IStateStoreContextConfigurer>,
                Lifestyle.Scoped);
            container.Register<IUserRightsProvider, UserRightsProvider>(Lifestyle.Scoped);

            container.RegisterConditional(typeof(IGetRequestHandler<,>),
                typeof(GetRequestHandler<,>), Lifestyle.Scoped,
                c => !c.Handled);
            container.RegisterConditional(
                typeof(IListRequestHandler<,,>),
                typeof(ListRequestHandler<,,>),
                Lifestyle.Scoped,
                c => !c.Handled);

            container.RegisterConditional(
                typeof(IEntityOperationRequestHandler<>),
                typeof(EntityOperationRequestHandler<>),
                Lifestyle.Scoped,
                c => !c.Handled);
            container.RegisterConditional(
                typeof(IOperationRequestHandler<>),
                typeof(OperationRequestHandler<>),
                Lifestyle.Scoped,
                c => !c.Handled);
            container.Register(typeof(ICreateEntityRequestHandler<>), typeof(CreateEntityRequestHandler<>), Lifestyle.Scoped);

            // ReSharper disable once PossiblyMistakenUseOfParamsMethod
            container.Register(typeof(ISingleEntitySpecBuilder<,>), typeof(TModule).Assembly);
            container.Register(typeof(IListEntitySpecBuilder<,>), typeof(TModule).Assembly);

            //container.RegisterConditional(typeof(ISingleEntitySpecBuilder<,>), typeof(GenericResourceSpecBuilder<>),
            //    c=> !c.Handled);

            container.Collection.Register(typeof(IHandleMessages<>), typeof(TModule).Assembly);
            container.Register<IOperationDispatcher, OperationDispatcher>(Lifestyle.Scoped);
            container.RegisterInstance(serviceProvider.GetRequiredService<WorkflowOptions>());
            container.Register<IOperationManager, OperationManager>(Lifestyle.Scoped);

            container.ConfigureRebus(configurer =>
            {
                return configurer
                    .Serialization(s => s.UseEryphSettings())
                    .Transport(t =>
                        container.GetRequiredService<IRebusTransportConfigurer>().ConfigureAsOneWayClient(t))
                    .Options(x =>
                    {
                        x.RetryStrategy();
                        x.SetNumberOfWorkers(5);
                    })
                    .Logging(x => x.MicrosoftExtensionsLogging(container.GetInstance<ILoggerFactory>()))
                    .Start();
            });
        }
    }

    //public class LogValidator : ISecurityTokenValidator
    //{
    //    public bool CanReadToken(string securityToken)
    //    {
    //        return true;
    //    }

    //    public ClaimsPrincipal ValidateToken(string securityToken, TokenValidationParameters validationParameters,
    //        out SecurityToken validatedToken)
    //    {
    //        throw new NotImplementedException();
    //    }

    //    public bool CanValidateToken => true;
    //    public int MaximumTokenSizeInBytes { get; set; }
    //}
}