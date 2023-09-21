using System;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Dbosoft.Hosuto.Modules.Testing;
using Eryph.IdentityDb;
using FluentAssertions;
using Moq;
using Xunit;
using Client = Eryph.Modules.Identity.Models.V1.Client;

namespace Eryph.Modules.Identity.Test.Integration.Endpoints;

public class GetClientsTest : IClassFixture<IdentityModuleNoAuthFactory>
{
    private readonly WebModuleFactory<IdentityModule> _factory;

    public GetClientsTest(IdentityModuleNoAuthFactory factory)
    {
        _factory = factory;
    }


    private WebModuleFactory<IdentityModule> SetupClients()
    {


        var factory = _factory.WithModuleConfiguration(options =>
        {
            options.ConfigureContainer(container =>
            {
                container.Options.AllowOverridingRegistrations = true;
            });
        });

        return factory;
    }

    [Fact]
    public async Task Get_Client()
    {
        var factory = SetupClients();

        var result = await factory.CreateDefaultClient().GetFromJsonAsync<Client>("v1/clients/test2");
        result.Should().NotBeNull();
        result.Id.Should().Be("test2");

    }
}