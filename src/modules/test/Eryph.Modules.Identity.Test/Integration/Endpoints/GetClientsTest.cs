using System;
using System.Net;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Dbosoft.Hosuto.Modules.Testing;
using Eryph.Core;
using Eryph.Modules.AspNetCore.ApiProvider;
using Eryph.Modules.AspNetCore.TestBase;
using Eryph.Modules.Identity.Models.V1;
using Eryph.Modules.Identity.Services;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using SimpleInjector;
using SimpleInjector.Lifestyles;
using Xunit;

namespace Eryph.Modules.Identity.Test.Integration.Endpoints;

[Collection("Token certificate collection")]
public class GetClientsTest(
    WebModuleFactory<IdentityModule> factory,
    TokenCertificateFixture tokenCertificates)
    : IClassFixture<WebModuleFactory<IdentityModule>>
{
    private readonly WebModuleFactory<IdentityModule> _factory = factory.WithIdentityHost(tokenCertificates);

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
                    TenantId = EryphConstants.DefaultTenantId,
                    ClientId = "test2",
                    DisplayName = "Test Client 2",
                    Scopes = { "compute:write" },
                }, false, CancellationToken.None).GetAwaiter().GetResult();
            });
        });

        return factory;
    }

    [Fact]
    public async Task Get_Client()
    {
        var factory = SetupClients();

        var response = await factory.CreateDefaultClient()
            .SetEryphToken(EryphConstants.DefaultTenantId, EryphConstants.SystemClientId, "identity:read", false)
            .GetAsync("v1/clients/test2");

        response.Should().HaveStatusCode(HttpStatusCode.OK);

        var client = await response.Content.ReadFromJsonAsync<Client>(
            ApiJsonSerializerOptions.Options)
            ?? throw new InvalidOperationException("The response body could not be deserialized.");

        client.Should().NotBeNull();
        client.Id.Should().Be("test2");
        client.Name.Should().Be("Test Client 2");
        client.AllowedScopes.Should().Equal("compute:write");
    }
}
