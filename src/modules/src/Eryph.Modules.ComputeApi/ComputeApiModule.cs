using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Eryph.Core;
using Eryph.ModuleCore.Authorization;
using Eryph.Modules.AspNetCore;
using Eryph.Modules.AspNetCore.ApiProvider.Handlers;
using Eryph.Modules.AspNetCore.ApiProvider.Model;
using Eryph.Modules.ComputeApi.Handlers;
using Eryph.Modules.ComputeApi.Model.V1;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SimpleInjector;
using IEndpointResolver = Eryph.ModuleCore.IEndpointResolver;

namespace Eryph.Modules.ComputeApi;

[UsedImplicitly]
public class ComputeApiModule(IEndpointResolver endpointResolver)
    : ApiModule<ComputeApiModule>
{
    public override string Path => endpointResolver.GetEndpoint("compute").ToString();

    public override string ApiName => "Compute Api";
        
    public override string AudienceName => EryphConstants.Authorization.Audiences.ComputeApi;

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
        container.Register<IGetRequestHandler<StateDb.Model.Catlet, CatletConfiguration>,
            GetCatletConfigurationHandler>();
        container.Register<IGetRequestHandler<StateDb.Model.Catlet, Catlet>,
            GetCatletHandler>();
        container.Register<IListFilteredByProjectRequestHandler<ListFilteredByProjectRequest, Catlet, StateDb.Model.Catlet>,
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
        // Define compute API scopes that need policies
        var computeApiScopes = new[]
        {
            EryphConstants.Authorization.Scopes.CatletsRead,
            EryphConstants.Authorization.Scopes.CatletsWrite,
            EryphConstants.Authorization.Scopes.CatletsControl,
            EryphConstants.Authorization.Scopes.GenesRead,
            EryphConstants.Authorization.Scopes.GenesWrite,
            EryphConstants.Authorization.Scopes.ProjectsRead,
            EryphConstants.Authorization.Scopes.ProjectsWrite,
        };

        // Create policies for each scope using hierarchy-aware scope resolution
        foreach (var scope in computeApiScopes)
        {
            CreateScopePolicy(options, authority, scope);
        }
    }

    private static void CreateScopePolicy(AuthorizationOptions options, string authority, string requiredScope)
    {
        // Get all scopes that can satisfy this requirement (including higher-level scopes)
        var allowedScopes = ScopeHierarchy.GetGrantingScopes(requiredScope);
        
        options.AddPolicy(requiredScope,
            policy => policy.Requirements.Add(new HasScopeRequirement(
                authority,
                allowedScopes)));
    }
}
