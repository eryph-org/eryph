using System;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Dbosoft.Hosuto.Modules.Testing;
using Eryph.Modules.AspNetCore.ApiProvider.Model.V1;
using Eryph.Modules.ComputeApi;
using FluentAssertions;
using Xunit;

namespace Eryph.Modules.CommonApi.Tests.Integration.Endpoints;

public class GetOperationTest : IClassFixture<ApiModuleFactory>
{
    private readonly WebModuleFactory<ComputeApiModule> _factory;

    public GetOperationTest(ApiModuleFactory factory)
    {
        _factory = factory.SetupStateStore(context =>
        {
            context.Operations.Add(ExistingOperation);
        });
    }

    private static readonly StateDb.Model.Operation ExistingOperation = new()
    {
        Id = Guid.NewGuid()
    };


    [Fact]
    public async Task Get_Returns_Existing_Operation()
    {

        var result = await _factory.CreateDefaultClient().GetFromJsonAsync<Operation>($"v1/operations/{ExistingOperation.Id}");
        result.Should().NotBeNull();
        result!.Id.Should().Be(ExistingOperation.Id.ToString());


    }

    //[Fact]
    //public async Task Get_Returns_404_If_Not_Found()
    //{

    //    var opId = Guid.NewGuid();

    //    var response = await _factory.CreateDefaultClient().GetAsync(($"v1/operations/{opId}"));
    //    response.Should().HaveStatusCode(HttpStatusCode.NotFound);

    //}
    
}