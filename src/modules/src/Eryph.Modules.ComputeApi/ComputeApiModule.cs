using System;
using System.Net;
using Eryph.Modules.AspNetCore;
using Eryph.Modules.AspNetCore.ApiProvider.Handlers;
using Eryph.Modules.AspNetCore.ApiProvider.Model;
using Eryph.Modules.ComputeApi.Handlers;
using Eryph.Modules.ComputeApi.Model;
using Eryph.Modules.ComputeApi.Model.V1;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SimpleInjector;
using Catlet = Eryph.Modules.ComputeApi.Model.V1.Catlet;
using IEndpointResolver = Eryph.ModuleCore.IEndpointResolver;

namespace Eryph.Modules.ComputeApi
{
    [UsedImplicitly]
    public class ComputeApiModule : ApiModule<ComputeApiModule>
    {
        private readonly IEndpointResolver _endpointResolver;

        public ComputeApiModule(IEndpointResolver endpointResolver)
        {
            _endpointResolver = endpointResolver;
        }

        public override string Path => _endpointResolver.GetEndpoint("compute").ToString();

        public override string ApiName => "Compute Api";
        public override string AudienceName => "compute_api";

        public override void ConfigureServices(IServiceProvider serviceProvider, IServiceCollection services,
            IHostEnvironment env)
        {
            base.ConfigureServices(serviceProvider, services, env);

            var endpointResolver = serviceProvider.GetRequiredService<IEndpointResolver>();
            var authority = endpointResolver.GetEndpoint("identity").ToString();

            services.AddAuthorization(options => ConfigureScopes(options, authority));
        }

        public override void ConfigureContainer(IServiceProvider serviceProvider, Container container)
        {
            container.Register<IGetRequestHandler<StateDb.Model.Catlet, CatletConfiguration>,
                GetCatletConfigurationHandler>();
            container.Register<IGetRequestHandler<StateDb.Model.Catlet, Catlet>,
                GetCatletHandler>();
            container.Register<IListRequestHandler<ListRequest, Catlet, StateDb.Model.Catlet>,
                ListCatletHandler>();
            container.Register<IGetRequestHandler<StateDb.Model.Gene, GeneWithUsage>, GetGeneHandler>();
            container.Register<IGetRequestHandler<StateDb.Model.Project, VirtualNetworkConfiguration>,
                GetVirtualNetworksConfigurationHandler>();
            container.Register<IEntityOperationRequestHandler<StateDb.Model.VirtualDisk>,
                DeleteVirtualDiskHandler>();

            base.ConfigureContainer(serviceProvider, container);
        }

        public static void ConfigureScopes(AuthorizationOptions options, string authority)
        {
            options.AddPolicy("compute:catlets:read",
                policy => policy.Requirements.Add(new HasScopeRequirement(
                    authority,
                    "compute:catlets:read", "compute:catlets:write", "compute:read", "compute:write")));
            options.AddPolicy("compute:catlets:write",
                policy => policy.Requirements.Add(new HasScopeRequirement(
                    authority,
                    "compute:catlets:write", "compute:write")));

            options.AddPolicy("compute:catlets:control",
                policy => policy.Requirements.Add(new HasScopeRequirement(
                    authority,
                    "compute:catlets:control", "compute:catlets:write", "compute:write")));

            options.AddPolicy("compute:genes:read",
                policy => policy.Requirements.Add(new HasScopeRequirement(
                    authority,
                    "compute:genes:read", "compute:genes:write", "compute:read", "compute:write")));
            options.AddPolicy("compute:genes:write",
                policy => policy.Requirements.Add(new HasScopeRequirement(
                    authority,
                    "compute:genes:write", "compute:write")));

            options.AddPolicy("compute:projects:read",
                policy => policy.Requirements.Add(new HasScopeRequirement(
                    authority,
                    "compute:projects:read", "compute:projects:write", "compute:read", "compute:write")));
            options.AddPolicy("compute:projects:write",
                policy => policy.Requirements.Add(new HasScopeRequirement(
                    authority,
                    "compute:projects:write", "compute:write")));

        }
    }

}