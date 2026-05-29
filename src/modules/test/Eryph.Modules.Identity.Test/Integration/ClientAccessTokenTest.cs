using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Claims;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Eryph.Modules.Identity.Services;
using FluentAssertions;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
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

        (await act.Should().ThrowAsync<HttpRequestException>())
            .Which.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Client_Gets_No_Access_Token_with_legacy_token_endpoint_audience()
    {
        // OpenIddict 7 adopts the security change that rejects the pre-7.0 token-endpoint
        // assertion audience (and the plain JWT type). A client that still sends the legacy
        // format must be rejected, confirming the server enforces the issuer audience.
        var act = () => GetClientAccessToken(
            TestClientData.KeyFileString1, TestClientData.CertificateString1, legacyFormat: true);

        (await act.Should().ThrowAsync<HttpRequestException>())
            .Which.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Client_Gets_No_Access_Token_when_no_certificate_registered()
    {
        // A confidential client provisioned without a certificate has no registered key, so a
        // client_assertion cannot be validated and must be rejected. This is the native-validation
        // equivalent of the explicit certificate-presence gate the 4.x workaround enforced.
        var act = () => GetClientAccessToken(TestClientData.KeyFileString1, certString: null);

        (await act.Should().ThrowAsync<HttpRequestException>())
            .Which.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private async Task<string> GetClientAccessToken(string keyString, string? certString, bool legacyFormat = false)
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
            
        return await RequestAccessTokenWithKey(httpClient, "test-client", rsaParameters, ["test_scope"], legacyFormat);
    }

    // Mirrors the behaviour of the updated Eryph.IdentityModel client library against an
    // OpenIddict 7.0+ server: the client assertion uses the issuer (read from the discovery
    // document) as the audience and the standardized "client-authentication+jwt" token type.
    // Once the new client library is published, this can be replaced by a call to it.
    private static async Task<string> RequestAccessTokenWithKey(
        HttpClient httpClient, string clientId, RSAParameters rsaParameters, string[] scopes,
        bool legacyFormat = false)
    {
        var discovery = await httpClient.GetStringAsync(httpClient.BaseAddress + ".well-known/openid-configuration");
        using var configuration = JsonDocument.Parse(discovery);
        var root = configuration.RootElement;
        var issuer = root.GetProperty("issuer").GetString();
        var tokenEndpoint = root.GetProperty("token_endpoint").GetString();

        // The server must advertise that the issuer is expected as the client-assertion audience.
        root.GetProperty("eryph_client_assertion_audience").GetString().Should().Be("issuer");

        // legacyFormat reproduces a pre-OpenIddict-7 client: token-endpoint audience + plain JWT type.
        var assertion = legacyFormat
            ? CreateClientAssertion(clientId, tokenEndpoint!, rsaParameters, tokenType: null)
            : CreateClientAssertion(clientId, issuer!, rsaParameters, tokenType: "client-authentication+jwt");

        var properties = new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = clientId,
            ["client_assertion_type"] = "urn:ietf:params:oauth:client-assertion-type:jwt-bearer",
            ["client_assertion"] = assertion,
            ["scope"] = string.Join(" ", scopes),
        };

        var request = new HttpRequestMessage(HttpMethod.Post, tokenEndpoint)
        {
            Content = new FormUrlEncodedContent(properties),
        };

        var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseContentRead);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        using var token = JsonDocument.Parse(content);
        return token.RootElement.GetProperty("access_token").GetString();
    }

    private static string CreateClientAssertion(
        string clientId, string audience, RSAParameters rsaParameters, string? tokenType)
    {
        var tokenHandler = new JwtSecurityTokenHandler { TokenLifetimeInMinutes = 5 };
        var securityToken = tokenHandler.CreateJwtSecurityToken(
            issuer: clientId,
            audience: audience,
            subject: new ClaimsIdentity(
            [
                new Claim("sub", clientId),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            ]),
            signingCredentials: new SigningCredentials(new RsaSecurityKey(rsaParameters), "RS256"));

        if (!string.IsNullOrEmpty(tokenType))
            securityToken.Header["typ"] = tokenType;

        return tokenHandler.WriteToken(securityToken);
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
