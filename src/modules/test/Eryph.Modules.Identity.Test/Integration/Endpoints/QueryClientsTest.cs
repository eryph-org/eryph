using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Dbosoft.Hosuto.Modules.Testing;
using Eryph.Modules.AspNetCore.ApiProvider.Model;
using Eryph.Modules.Identity.Models.V1;
using Eryph.Modules.Identity.Services;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using SimpleInjector;
using SimpleInjector.Lifestyles;
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
        _factory = factory.WithIdentityHost(tokenCertificates)
            .WithoutAuthorization();
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
                    ClientId = "test1",
                }, false, CancellationToken.None).GetAwaiter().GetResult();
                _ = clientService.Add(new ClientApplicationDescriptor
                {
                    ClientId = "test2"
                }, false, CancellationToken.None).GetAwaiter().GetResult();

            });
        });

        return factory;
    }

    [Fact]
    public async Task Query_Clients()
    {
        var factory = SetupClients();

        var result = await factory.CreateDefaultClient().GetFromJsonAsync<ListEntitiesResponse<Client>>("v1/clients");
        result.Should().NotBeNull();
        result.Value.Count().Should().Be(2);
        result.Value.First().Id.Should().Be("test1");
        result.Value.Last().Id.Should().Be("test2");
    }
}
