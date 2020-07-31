using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json.Linq;

namespace Haipa.Modules.Identity.Test
{
    internal static class HttpClientRequestExtensions
    {
        public static async Task<string> GetClientAccessToken(this HttpClient httpClient, string clientName, X509Certificate2 clientCertificate, string scopes)
        {

            var request = new HttpRequestMessage(HttpMethod.Post, "connect/token");
            var jwt = CreateClientAuthJwt("http://localhost/connect/token", clientName, clientCertificate);

            request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = clientName,
                ["client_assertion_type"] = "urn:ietf:params:oauth:client-assertion-type:jwt-bearer",
                ["client_assertion"] = jwt,
                //["scopes"] = scopes
            });

            var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseContentRead);
            var content = await response.Content.ReadAsStringAsync();
            response.EnsureSuccessStatusCode();

            var payload = JObject.Parse(content);
            if (payload["error"] != null)
            {
                throw new InvalidOperationException("An error occurred while retrieving an access token.");
            }

            return payload["access_token"].ToString();
        }

        private static string CreateClientAuthJwt(string audience, string issuerName, X509Certificate2 issuerCert)
        {
            // set exp to 5 minutes
            var tokenHandler = new JwtSecurityTokenHandler { TokenLifetimeInMinutes = 5 };
            var securityToken = tokenHandler.CreateJwtSecurityToken(
                // iss must be the client_id of our application
                issuer: issuerName,
                // aud must be the identity provider (token endpoint)
                audience: audience,
                // sub must be the client_id of our application
                subject: new ClaimsIdentity(
                    new List<Claim> { new Claim("sub", issuerName) }),
                // sign with the private key (using RS256 for IdentityServer)
                signingCredentials: new SigningCredentials(
                    new X509SecurityKey(issuerCert), "RS256")
            );

            return tokenHandler.WriteToken(securityToken);
 
        }

        public static async Task<string> GetResourceAsync(this HttpClient client, string token, string resource)
        {
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, resource);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            HttpResponseMessage response = await client.SendAsync(request, HttpCompletionOption.ResponseContentRead);
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadAsStringAsync();
        }
    }
}