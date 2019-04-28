using IdentityModel.OidcClient;
using Microsoft.Net.Http.Server;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Identity.TestClient
{

    public class Program
    {
        //public static void Main(string[] args) => MainAsync(args).GetAwaiter().GetResult();
        public static void Main(string[] args)
        {
            MainAsync2().GetAwaiter().GetResult();
        }

        public static async Task MainAsync2()
        {
            Console.WriteLine("+-----------------------+");
            Console.WriteLine("|  Sign in with OIDC    |");
            Console.WriteLine("+-----------------------+");
            Console.WriteLine("");
            Console.WriteLine("Press any key to sign in...");
            Console.ReadKey();

            await SignInAsync();

            Console.ReadKey();
        }

        private static async Task SignInAsync()
        {
            // create an HttpListener to listen for requests on that redirect URI.
            string redirectUri = string.Format("http://127.0.0.1:7890");

            WebListenerSettings settings = new WebListenerSettings();
            settings.UrlPrefixes.Add(redirectUri);
            WebListener http = new WebListener(settings);
            http.Start();
            Console.WriteLine("Listening..");

            OidcClientOptions options = new OidcClientOptions
            {
                Authority = "https://localhost:62189/identity",
                RedirectUri = redirectUri,
                ClientId = "console",
                //ClientSecret = "peng",
                Scope = "openid offline_access",
                Flow = OidcClientOptions.AuthenticationFlow.Hybrid
            };

            OidcClient client = new OidcClient(options);
            AuthorizeState state = await client.PrepareLoginAsync();

            OpenBrowser(state.StartUrl);

            RequestContext context = await http.AcceptAsync();
            string formData = GetRequestPostData(context.Request);

            if (formData == null)
            {
                Console.WriteLine("Invalid response");
                return;
            }

            await SendResponseAsync(context.Response);

            LoginResult result = await client.ProcessResponseAsync(formData, state);

            ShowResult(result);
        }

        private static void ShowResult(LoginResult result)
        {
            if (result.IsError)
            {
                Console.WriteLine("\n\nError:\n{0}", result.Error);
                return;
            }

            Console.WriteLine("\n\nClaims:");
            foreach (System.Security.Claims.Claim claim in result.User.Claims)
            {
                Console.WriteLine("{0}: {1}", claim.Type, claim.Value);
            }

            Console.WriteLine($"\nidentity token: {result.IdentityToken}");
            Console.WriteLine($"access token:   {result.AccessToken}");
            Console.WriteLine($"refresh token:  {result?.RefreshToken ?? "none"}");
        }

        private static async Task SendResponseAsync(Response response)
        {
            string responseString = $"<html><head></head><body>Please return to the app.</body></html>";
            byte[] buffer = Encoding.UTF8.GetBytes(responseString);

            response.ContentLength = buffer.Length;

            Stream responseOutput = response.Body;
            await responseOutput.WriteAsync(buffer, 0, buffer.Length);
            responseOutput.Flush();
        }

        public static void OpenBrowser(string url)
        {
            try
            {
                Process.Start(url);
            }
            catch
            {
                // hack because of this: https://github.com/dotnet/corefx/issues/10361
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    url = url.Replace("&", "^&");
                    Process.Start(new ProcessStartInfo("cmd", $"/c start {url}") { CreateNoWindow = true });
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    Process.Start("xdg-open", url);
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    Process.Start("open", url);
                }
                else
                {
                    throw;
                }
            }
        }

        public static string GetRequestPostData(Request request)
        {
            if (!request.HasEntityBody)
            {
                return null;
            }

            using (Stream body = request.Body)
            {
                using (StreamReader reader = new StreamReader(body))
                {
                    return reader.ReadToEnd();
                }
            }
        }

        public static async Task MainAsync(string[] args)
        {
            HttpClient client = new HttpClient();

            try
            {
                string token = await GetTokenAsync(client);
                Console.WriteLine("Access token: {0}", token);
                Console.WriteLine();

                string resource = await GetResourceAsync(client, token);
                Console.WriteLine("API response: {0}", resource);
                Console.ReadLine();
            }
            catch (System.Net.Http.HttpRequestException ex)
            {
                StringBuilder builder = new StringBuilder();
                builder.AppendLine("+++++++++++++++++++++");
                builder.AppendLine(ex.Message);
                builder.AppendLine(ex.InnerException.Message);
                builder.AppendLine("Make sure you started the authorization server.");
                builder.AppendLine("+++++++++++++++++++++");
                Console.WriteLine(builder.ToString());
            }
        }

        public static async Task<string> GetTokenAsync(HttpClient client)
        {
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, "http://localhost:62189/identity/connect/token");
            request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = "console",
                ["client_secret"] = "peng",
                //["scope"] = "identity:apps:read:all"
            });

            HttpResponseMessage response = await client.SendAsync(request, HttpCompletionOption.ResponseContentRead);
            response.EnsureSuccessStatusCode();

            JObject payload = JObject.Parse(await response.Content.ReadAsStringAsync());
            if (payload["error"] != null)
            {
                throw new InvalidOperationException("An error occurred while retrieving an access token.");
            }

            return (string)payload["access_token"];
        }

        public static async Task<string> GetResourceAsync(HttpClient client, string token)
        {
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, "http://localhost:62189/identity/userinfo");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            HttpResponseMessage response = await client.SendAsync(request, HttpCompletionOption.ResponseContentRead);
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadAsStringAsync();
        }

    }



}
