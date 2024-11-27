using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Eryph.IdentityModel.Clients;
using Eryph.Modules.Identity.Services;
using FluentAssertions;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SimpleInjector;
using SimpleInjector.Lifestyles;
using Xunit;
using Xunit.Abstractions;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using Dbosoft.Hosuto.Modules.Testing;
using OpenIddict.Abstractions;

namespace Eryph.Modules.Identity.Test.Integration;

[Collection("Token certificate collection")]
public class ClientAccessTokenTest(
    WebModuleFactory<IdentityModule> factory,
    TokenCertificateFixture tokenCertificates,
    ITestOutputHelper testOutputHelper)
    : IClassFixture<WebModuleFactory<IdentityModule>>
{
    [Fact]
    public async Task Valid_Client_Gets_Access_Token_from_key()
    {
        var token = await GetClientAccessToken(TestClientData.KeyFileString1, TestClientData.CertificateString1);

        token.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Valid_Client_Gets_Access_Token_from_shared_key()
    {
        var token = await GetSharedKeyAccessToken("1234", "1234");

        token.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Client_Gets_No_Access_Token_from_other_shared_key()
    {
        var act = () => GetSharedKeyAccessToken("1234", "12345");

        (await act.Should().ThrowAsync<HttpRequestException>())
            .Which.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Client_Gets_No_Access_Token_from_other_key()
    {
        var act = () => GetClientAccessToken(TestClientData.KeyFileString1, TestClientData.CertificateString2);

        (await act.Should().ThrowAsync<AccessTokenException>())
            .WithMessage("Could not retrieve an access token. The server responded with Unauthorized.");
    }

    private async Task<string> GetClientAccessToken(string keyString, string certString)
    {
        using var keyPair = RSA.Create();
        keyPair.ImportFromPem(keyString);
        var rsaParameters = keyPair.ExportParameters(true);


        using var httpClient = factory
            .WithIdentityHost(tokenCertificates)
            .WithXunitLogging(testOutputHelper)
            .WithModuleConfiguration(o =>
            {
                o.Configure(ctx =>
                {
                    var container = ctx.Services.GetRequiredService<Container>();
                    using var scope = AsyncScopedLifestyle.BeginScope(container);
                    
                    var scopeManager = scope.GetInstance<IOpenIddictScopeManager>();
                    scopeManager.CreateAsync(new OpenIddictScopeDescriptor
                    {
                        Name = "test_scope",
                        Resources = { "test_audience" }
                    }).GetAwaiter().GetResult();

                    var clientService = scope.GetRequiredService<IClientService>();
                    clientService.Add(new ClientApplicationDescriptor
                    {
                        ClientId = "test-client",
                        Certificate = certString,
                        Scopes = { "test_scope" }
                    }, false, CancellationToken.None).GetAwaiter().GetResult();
                });
            })
            .CreateDefaultClient();
            
        var response = await httpClient.GetClientAccessToken(
            new Uri(httpClient.BaseAddress + "connect/token"),
            "test-client",
            rsaParameters,
            ["test_scope"]);
        return response.AccessToken;
    }

    private async Task<string> GetSharedKeyAccessToken(string clientSharedKey, string usedSharedKey)
    {
        using var httpClient = factory.WithIdentityHost(tokenCertificates)
            .WithXunitLogging(testOutputHelper)
            .WithModuleConfiguration(o =>
            {
                o.Configure(ctx =>
                {
                    var container = ctx.Services.GetRequiredService<Container>();
                    using var scope = AsyncScopedLifestyle.BeginScope(container);
                    var clientService = scope.GetRequiredService<IClientService>();
                    clientService.Add(new ClientApplicationDescriptor
                    {
                        ClientId = "test-client",
                        Certificate = TestClientData.CertificateString1,
                        ClientSecret = clientSharedKey,
                        Scopes = { "compute_api" }
                    }, false, CancellationToken.None).GetAwaiter().GetResult();
                });
            })
            .CreateDefaultClient();

        var fullAddress = httpClient.BaseAddress;
        var audience = fullAddress + "connect/token";

        var properties = new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = "test-client",
        };

        var authenticationString = $"test-client:{usedSharedKey}";
        var base64EncodedAuthenticationString = Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes(authenticationString));
        var request = new HttpRequestMessage(HttpMethod.Post, audience)
        {
            Content = new FormUrlEncodedContent(properties),
            Headers = { Authorization = new AuthenticationHeaderValue("Basic", base64EncodedAuthenticationString)
            }
        };

        var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseContentRead)
            .ConfigureAwait(false);
        var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        try
        {
            var payload = JwtPayload.Deserialize(content);
            if (payload.TryGetValue("error", out _))
                throw new InvalidOperationException();

            payload.TryGetValue("access_token", out var accessToken);

            return accessToken as string;
        }
        catch
        {
            throw new InvalidOperationException("An error occurred while retrieving an access token.");
        }
    }
}
