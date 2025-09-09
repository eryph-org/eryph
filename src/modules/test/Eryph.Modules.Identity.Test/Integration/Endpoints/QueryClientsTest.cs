using Dbosoft.Hosuto.Modules.Testing;
using Eryph.Core;
using Eryph.Modules.AspNetCore.ApiProvider;
using Eryph.Modules.AspNetCore.ApiProvider.Model;
using Eryph.Modules.AspNetCore.TestBase;
using Eryph.Modules.Identity.Models.V1;
using Eryph.Modules.Identity.Services;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using SimpleInjector;
using SimpleInjector.Lifestyles;
using System.Net;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Eryph.Modules.Identity.Test.Integration.Endpoints;

[Collection("Token certificate collection")]
public class QueryClientsTest : IClassFixture<WebModuleFactory<IdentityModule>>
{
    private readonly WebModuleFactory<IdentityModule> _factory;

    public QueryClientsTest(
        WebModuleFactory<IdentityModule> factory,
        TokenCertificateFixture tokenCertificates)
    {
        _factory = factory.WithIdentityHost(tokenCertificates);
    }

    private WebModuleFactory<IdentityModule> SetupClients()
    {
        var factory = _factory.WithModuleConfiguration(options =>
        {
            options.Configure(ctx =>
            {
                var container = ctx.Services.GetRequiredService<Container>();
                using var scope = AsyncScopedLifestyle.BeginScope(container);
                var clientService = scope.GetRequiredService<IClientService>();
                _ = clientService.Add(new ClientApplicationDescriptor
                {
                    TenantId = EryphConstants.DefaultTenantId,
                    ClientId = "test1",
                    DisplayName = "Test Client 1",
                    Scopes = { EryphConstants.Authorization.Scopes.ComputeWrite }
                }, false, CancellationToken.None).GetAwaiter().GetResult();
                _ = clientService.Add(new ClientApplicationDescriptor
                {
                    TenantId = EryphConstants.DefaultTenantId,
                    ClientId = "test2",
                    DisplayName = "Test Client 2",
                    Scopes = { EryphConstants.Authorization.Scopes.ComputeRead }
                }, false, CancellationToken.None).GetAwaiter().GetResult();
            });
        });

        return factory;
    }

    [Fact]
    public async Task Query_Clients()
    {
        var factory = SetupClients();

        var response = await factory.CreateDefaultClient()
            .SetEryphToken(EryphConstants.DefaultTenantId, EryphConstants.SystemClientId, "identity:read", false)
            .GetAsync("v1/clients");
        response.Should().HaveStatusCode(HttpStatusCode.OK);

        var clients = await response.Content.ReadFromJsonAsync<ListResponse<Client>>(
            options: ApiJsonSerializerOptions.Options);

        clients.Should().NotBeNull();
        clients.Value.Should().SatisfyRespectively(
            client =>
            {
                client.Id.Should().Be("test1");
                client.Name.Should().Be("Test Client 1");
                client.AllowedScopes.Should().Equal(EryphConstants.Authorization.Scopes.ComputeWrite);
            },
            client =>
            {
                client.Id.Should().Be("test2");
                client.Name.Should().Be("Test Client 2");
                client.AllowedScopes.Should().Equal(EryphConstants.Authorization.Scopes.ComputeRead);
            });
    }
}
