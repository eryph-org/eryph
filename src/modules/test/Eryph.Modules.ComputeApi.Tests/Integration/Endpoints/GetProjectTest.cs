using System;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Dbosoft.Hosuto.Modules.Hosting;
using Eryph.Core;
using Eryph.Modules.AspNetCore;
using Eryph.StateDb;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using SimpleInjector;
using SimpleInjector.Lifestyles;
using Xunit;
using Project = Eryph.Modules.AspNetCore.ApiProvider.Model.V1.Project;

namespace Eryph.Modules.ComputeApi.Tests.Integration.Endpoints;

public class GetProjectTest : IClassFixture<WebModuleFactory<ComputeApiModule>>
{
    private readonly WebModuleFactory<ComputeApiModule> _factory;

    public GetProjectTest(WebModuleFactory<ComputeApiModule> factory)
    {
        _factory = factory.WithApiHost().WithModuleConfiguration(o =>
        {
            o.Configure(sp =>
            {
                var container = sp.Services.GetRequiredService<Container>();
                using var scope = AsyncScopedLifestyle.BeginScope(container);

                var stateStore = scope.GetInstance<StateStoreContext>();

                stateStore.Projects.Add(ExistingProject);

                stateStore.SaveChanges();
            });
        }).WithModuleConfiguration(o =>
            o.Configure(ctx =>
            {
                _ = ctx.Advanced;
                var context = (ISimpleInjectorModuleContext)ctx;
                context.Container.Options.AllowOverridingRegistrations = true;
                context.Container.Register<IUserRightsProvider, TestingUserRightsProvider>();
            }));


    }

    private static readonly StateDb.Model.Project ExistingProject = new()
    {
        Id = Guid.NewGuid(),
        TenantId = EryphConstants.DefaultTenantId
    };


    [Fact]
    public async Task Get_Returns_Existing_Project()
    {

        var result = await _factory.CreateDefaultClient().GetFromJsonAsync<Project>($"v1/projects/{ExistingProject.Id}");
        result.Should().NotBeNull();
        result!.Id.Should().Be(ExistingProject.Id.ToString());


    }

    [Fact]
    public async Task Get_Returns_404_If_Not_Found()
    {

        var opId = Guid.NewGuid();

        var response = await _factory.CreateDefaultClient().GetAsync(($"v1/operations/{opId}"));
        response.Should().HaveStatusCode(HttpStatusCode.NotFound);

    }

}