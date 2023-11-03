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
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.OpenSsl;
using Org.BouncyCastle.Security;
using SimpleInjector;
using SimpleInjector.Lifestyles;
using Xunit;
using Xunit.Abstractions;
using System.Net.Http.Headers;

namespace Eryph.Modules.Identity.Test.Integration
{
    public class ClientAccessTokenTest 
    {
        private readonly ITestOutputHelper _outputHelper;

        public ClientAccessTokenTest(ITestOutputHelper outputHelper)
        {
            _outputHelper = outputHelper;
        }

        [Fact]
        public async Task Valid_Client_Gets_Access_Token_from_key()
        {
            Assert.NotNull(await GetClientAccessToken(TestClientData.KeyFileString1, TestClientData.CertificateString1));
        }

        [Fact]
        public async Task Valid_Client_Gets_Access_Token_from_shared_key()
        {
            Assert.NotNull(await GetSharedKeyAccessToken("1234", "1234"));
        }

        [Fact]
        public async Task Client_Gets_No_Access_Token_from_other_shared_key()
        {

            var ex = await Assert.ThrowsAsync<HttpRequestException>(async () =>
                await GetSharedKeyAccessToken("1234", "12345"));

            ex.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        }

        [Fact]
        public async Task Client_Gets_No_Access_Token_from_other_key()
        {

            var ex = await Assert.ThrowsAsync<HttpRequestException>(async () =>
                await GetClientAccessToken(TestClientData.KeyFileString2, TestClientData.CertificateString1));

            ex.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        }

        private async Task<string> GetClientAccessToken(string keyString, string certString)
        {

            var keyPair =
                (AsymmetricCipherKeyPair)new PemReader(new StringReader(keyString)).ReadObject();
            var rsaParameters = DotNetUtilities.ToRSAParameters(keyPair.Private as RsaPrivateCrtKeyParameters);

            
            var factory = new IdentityModuleFactory().WithWebHostBuilder(c =>
            {
                c.ConfigureTestServices(
                    services =>
                    {
                        services.AddLogging((builder) => builder.AddXUnit(_outputHelper));


                    });

                }).WithModuleConfiguration(o=>
            {
                o.Configure(ctx =>
                {
                    var container = ctx.Services.GetRequiredService<Container>();
                    using var scope = AsyncScopedLifestyle.BeginScope(container);
                    var clientService = scope.GetRequiredService<IClientService>();
                    clientService.Add(new ClientApplicationDescriptor
                    {
                        ClientId = "test-client",
                        Certificate = certString,
                        Scopes = { "compute_api" }
                    }, false, CancellationToken.None).GetAwaiter().GetResult();
                });
            });
            
            var response = await factory.CreateDefaultClient()
                .GetClientAccessToken("test-client", rsaParameters, new[] { "compute_api" });
            return response.AccessToken;
        }


        private async Task<string> GetSharedKeyAccessToken(string clientSharedKey, string usedSharedKey)
        {

            var factory = new IdentityModuleFactory().WithWebHostBuilder(c =>
            {
                c.ConfigureTestServices(
                    services =>
                    {
                        services.AddLogging((builder) => builder.AddXUnit(_outputHelper));


                    });

            }).WithModuleConfiguration(o =>
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
            });

            var httpClient = factory.CreateDefaultClient();

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
}