using System;
using System.Collections.Generic;
using Eryph.Core;
using Eryph.Messages.Components;
using Eryph.ModuleCore;
using Eryph.ModuleCore.Authorization;
using Eryph.ModuleCore.Components;
using Eryph.Modules.AspNetCore;
using Eryph.Modules.AspNetCore.ApiProvider;
using Eryph.Modules.AspNetCore.ApiProvider.Handlers;
using Eryph.Modules.AspNetCore.ApiProvider.Model;
using Eryph.Modules.ComputeApi.Handlers;
using Eryph.Modules.ComputeApi.Model.V1;
using Eryph.Rebus;
using Eryph.StateDb.Model;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SimpleInjector;
using SimpleInjector.Integration.ServiceCollection;
using Catlet = Eryph.StateDb.Model.Catlet;
using CatletSpecification = Eryph.StateDb.Model.CatletSpecification;
using Gene = Eryph.StateDb.Model.Gene;
using IEndpointResolver = Eryph.ModuleCore.IEndpointResolver;
using VirtualDisk = Eryph.StateDb.Model.VirtualDisk;

namespace Eryph.Modules.ComputeApi;

[UsedImplicitly]
public class ComputeApiModule(IEndpointResolver endpointResolver)
    : ApiModule<ComputeApiModule>
{
    public override string Path => endpointResolver.GetEndpoint("compute").ToString();

    public override string ApiName => "Compute Api";

    public override string AudienceName => EryphConstants.Authorization.Audiences.ComputeApi;

    public override void AddSimpleInjector(SimpleInjectorAddOptions options)
    {
        base.AddSimpleInjector(options);

        // Register as a deployment component and advertise the compute endpoint. Uniform
        // across packagings (eryph-zero included): every component registers with the
        // controller so the deployment has a complete, mandatory service catalog.
        options.AddComponentRegistration(
            ComponentType.ComputeApi,
            $"{QueueNames.ApiServices}.{ComponentIdentity.GetLocalHostId()}",
            new Dictionary<string, string>
            {
                ["compute"] = endpointResolver.GetEndpoint("compute").ToString(),
            });
    }

    public override void ConfigureServices(
        IServiceProvider serviceProvider,
        IServiceCollection services,
        IHostEnvironment env)
    {
        base.ConfigureServices(serviceProvider, services, env);

        var authority = endpointResolver.GetEndpoint("identity").ToString();
        services.AddAuthorization(options => ConfigureScopes(options, authority));
    }

    public override void ConfigureContainer(IServiceProvider serviceProvider, Container container)
    {
        // IAgentChannelForwarder (the SSH channel data plane) is supplied by the host, like the bus
        // transport and state store — see each host's HostComputeApiModuleExtensions.

        container.Register<IGetRequestHandler<Catlet, CatletConfiguration>,
            GetCatletConfigurationHandler>();
        container.Register<IGetRequestHandler<Catlet, Model.V1.Catlet>,
            GetCatletHandler>();
        container.Register<IListFilteredByProjectRequestHandler<ListFilteredByProjectRequest, Model.V1.Catlet, Catlet>,
            ListCatletHandler>();
        container.Register<IGetRequestHandler<Gene, GeneWithUsage>, GetGeneHandler>();
        container.Register<IGetRequestHandler<Project, VirtualNetworkConfiguration>,
            GetVirtualNetworksConfigurationHandler>();
        container.Register<IEntityOperationRequestHandler<VirtualDisk>,
            DeleteVirtualDiskHandler>();
        container.Register<IGetRequestHandler<CatletSpecification, Model.V1.CatletSpecification>,
            GetCatletSpecificationHandler>();
        container
            .Register<IListFilteredByProjectRequestHandler<ListFilteredByProjectRequest, Model.V1.CatletSpecification,
                    CatletSpecification>,
                ListCatletSpecificationHandler>();

        container.Register<IOperationCancellationDispatcher, OperationCancellationDispatcher>(Lifestyle.Scoped);

        base.ConfigureContainer(serviceProvider, container);
    }

    public static void ConfigureScopes(AuthorizationOptions options, string authority)
    {
        // Create policies for each scope using hierarchy-aware scope resolution
        foreach (var scope in ScopeDefinitions.ComputeApiScopes) CreateScopePolicy(options, authority, scope);
    }

    private static void CreateScopePolicy(AuthorizationOptions options, string authority, string requiredScope)
    {
        options.FallbackPolicy = new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUserOrSwaggerEndpoint()
            .Build();

        // Get all scopes that can satisfy this requirement (including higher-level scopes)
        var allowedScopes = ScopeHierarchy.GetGrantingScopes(requiredScope);

        options.AddPolicy(requiredScope,
            policy => policy.Requirements.Add(new HasScopeRequirement(
                authority,
                allowedScopes)));
    }
}
