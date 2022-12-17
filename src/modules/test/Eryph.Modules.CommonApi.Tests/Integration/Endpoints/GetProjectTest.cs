using System;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Dbosoft.Hosuto.Modules.Testing;
using Eryph.Modules.AspNetCore.ApiProvider.Model.V1;
using FluentAssertions;
using Xunit;

namespace Eryph.Modules.CommonApi.Tests.Integration.Endpoints;

public class GetProjectTest : IClassFixture<ApiModuleFactory>
{
    private readonly WebModuleFactory<CommonApiModule> _factory;

    public GetProjectTest(ApiModuleFactory factory)
    {
        _factory = factory.SetupStateStore(context =>
        {
            context.Projects.Add(ExistingProject);
        });
    }

    private static readonly StateDb.Model.Project ExistingProject = new()
    {
        Id = Guid.NewGuid()
    };


    [Fact]
    public async Task Get_Returns_Existing_Project()
    {

        var result = await _factory.CreateDefaultClient().GetFromJsonAsync<Project>($"v1/projects/{ExistingProject.Id}");
        result.Should().NotBeNull();
        result!.Id.Should().Be(ExistingProject.Id.ToString());


    }

    //[Fact]
    //public async Task Get_Returns_404_If_Not_Found()
    //{

    //    var opId = Guid.NewGuid();

    //    var response = await _factory.CreateDefaultClient().GetAsync(($"v1/operations/{opId}"));
    //    response.Should().HaveStatusCode(HttpStatusCode.NotFound);

    //}
    
}