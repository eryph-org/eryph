using System;
using System.Collections.Generic;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Dbosoft.Hosuto.Modules.Testing;
using Dbosoft.IdentityServer;
using Dbosoft.IdentityServer.Storage.Models;
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


    private WebModuleFactory<IdentityModule> SetupClients(Action<Mock<IIdentityServerClientService>> configure)
    {
        var serviceMock = new Mock<IIdentityServerClientService>();
        configure(serviceMock);


        var factory = _factory.WithModuleConfiguration(options =>
        {
            options.ConfigureContainer(container =>
            {
                container.Options.AllowOverridingRegistrations = true;
                container.RegisterInstance(serviceMock.Object);
            });
        });

        return factory;
    }

    [Fact]
    public async Task Get_Client()
    {
        var factory = SetupClients(mock =>
        {
            mock.Setup(x => x.GetClient("test2")).ReturnsAsync(
                new Dbosoft.IdentityServer.Storage.Models.Client()
                {
                    ClientId = "test2",
                    ClientSecrets = new List<Secret>(new[]
                    {
                        new Secret
                        {
                            Type = IdentityServerConstants.SecretTypes.X509CertificateBase64,
                            Value = TestClientData.CertificateString
                        }
                    })

                });
        });

        var result = await factory.CreateDefaultClient().GetFromJsonAsync<Client>("v1/clients/test2");
        result.Should().NotBeNull();
        result.Id.Should().Be("test2");

    }
}