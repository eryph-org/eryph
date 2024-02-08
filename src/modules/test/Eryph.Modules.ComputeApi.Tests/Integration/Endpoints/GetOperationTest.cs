using System;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Eryph.StateDb.Model;
using FluentAssertions;
using Xunit;

namespace Eryph.Modules.ComputeApi.Tests.Integration.Endpoints;

public class GetOperationTest : IClassFixture<WebModuleFactory<ComputeApiModule>>
{
    private readonly WebModuleFactory<ComputeApiModule> _factory;

    public GetOperationTest(WebModuleFactory<ComputeApiModule> factory)
    {
        _factory = factory;
    }

    private static readonly OperationModel ExistingOperation = new()
    {
        Id = Guid.NewGuid()
    };

    private WebModuleFactory<ComputeApiModule> FactoryWithOperation()
    {
        return _factory.WithApiHost().SetupStateStore(context =>
        {
            context.Operations.Add(ExistingOperation);
        });

    }

    [Fact]
    public async Task Get_Returns_Existing_Operation()
    {

        var result = await FactoryWithOperation().CreateDefaultClient().GetFromJsonAsync<OperationModel>($"v1/operations/{ExistingOperation.Id}");
        result.Should().NotBeNull();
        result!.Id.Should().Be(ExistingOperation.Id.ToString());


    }

    [Fact]
    public async Task Get_Returns_404_If_Not_Found()
    {

        var opId = Guid.NewGuid();

        var response = await FactoryWithOperation().CreateDefaultClient().GetAsync(($"v1/operations/{opId}"));
        response.Should().HaveStatusCode(HttpStatusCode.NotFound);

    }

}