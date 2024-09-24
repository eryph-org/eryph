using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Dbosoft.Hosuto.Modules.Testing;
using Eryph.Modules.Identity.Models.V1;
using Eryph.Modules.Identity.Services;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using SimpleInjector;
using SimpleInjector.Lifestyles;
using Xunit;

namespace Eryph.Modules.Identity.Test.Integration.Endpoints;

[Collection("Token certificate collection")]
public class GetClientsTest : IClassFixture<WebModuleFactory<IdentityModule>>
{
    private readonly WebModuleFactory<IdentityModule> _factory;

    public GetClientsTest(
        WebModuleFactory<IdentityModule> factory,
        TokenCertificateFixture tokenCertificates)
    {
        _factory = factory.WithIdentityHost(tokenCertificates)
            .WithoutAuthorization();
    }

    private WebModuleFactory<IdentityModule> SetupClients()
    {
        var factory = _factory.WithModuleConfiguration(o =>
        {
            o.Configure(ctx =>
            {
                var container = ctx.Services.GetRequiredService<Container>();
                using var scope = AsyncScopedLifestyle.BeginScope(container);
                var clientService = scope.GetRequiredService<IClientService>();
                _ = clientService.Add(new ClientApplicationDescriptor
                {
                    ClientId = "test2"
                }, false, CancellationToken.None).GetAwaiter().GetResult();
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
