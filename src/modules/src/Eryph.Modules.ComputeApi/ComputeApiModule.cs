using System;
using System.Net;
using Eryph.Core;
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
        options.AddPolicy(EryphConstants.Authorization.Policies.CatletsRead,
            policy => policy.Requirements.Add(new HasScopeRequirement(
                authority,
                EryphConstants.Authorization.Scopes.CatletsRead,
                EryphConstants.Authorization.Scopes.CatletsWrite,
                EryphConstants.Authorization.Scopes.ComputeRead,
                EryphConstants.Authorization.Scopes.ComputeWrite)));
        options.AddPolicy(EryphConstants.Authorization.Policies.CatletsWrite,
            policy => policy.Requirements.Add(new HasScopeRequirement(
                authority,
                EryphConstants.Authorization.Scopes.CatletsWrite,
                EryphConstants.Authorization.Scopes.ComputeWrite)));
        options.AddPolicy(EryphConstants.Authorization.Policies.CatletsControl,
            policy => policy.Requirements.Add(new HasScopeRequirement(
                authority,
                EryphConstants.Authorization.Scopes.CatletsControl,
                EryphConstants.Authorization.Scopes.CatletsWrite,
                EryphConstants.Authorization.Scopes.ComputeWrite)));

        options.AddPolicy(EryphConstants.Authorization.Policies.GenesRead,
            policy => policy.Requirements.Add(new HasScopeRequirement(
                authority,
                EryphConstants.Authorization.Scopes.GenesRead,
                EryphConstants.Authorization.Scopes.GenesWrite,
                EryphConstants.Authorization.Scopes.ComputeRead,
                EryphConstants.Authorization.Scopes.ComputeWrite)));
        options.AddPolicy(EryphConstants.Authorization.Policies.GenesWrite,
            policy => policy.Requirements.Add(new HasScopeRequirement(
                authority,
                EryphConstants.Authorization.Scopes.GenesWrite,
                EryphConstants.Authorization.Scopes.ComputeWrite)));

        options.AddPolicy(EryphConstants.Authorization.Policies.ProjectsRead,
            policy => policy.Requirements.Add(new HasScopeRequirement(
                authority,
                EryphConstants.Authorization.Scopes.ProjectsRead,
                EryphConstants.Authorization.Scopes.ProjectsWrite,
                EryphConstants.Authorization.Scopes.ComputeRead,
                EryphConstants.Authorization.Scopes.ComputeWrite)));
        options.AddPolicy(EryphConstants.Authorization.Policies.ProjectsWrite,
            policy => policy.Requirements.Add(new HasScopeRequirement(
                authority,
                EryphConstants.Authorization.Scopes.ProjectsWrite,
                EryphConstants.Authorization.Scopes.ComputeWrite)));
    }
}
