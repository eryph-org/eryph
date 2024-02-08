using System;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Eryph.Modules.ComputeApi.Tests.Integration;

public class MockBackchannel : HttpMessageHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.RequestUri.AbsoluteUri.Equals("https://inmemory.microsoft.com/common/.well-known/openid-configuration"))
        {
            return await EmbeddedResourceReader.GetOpenIdConfigurationAsResponseMessage("microsoft-openid-config.json");
        }
        if (request.RequestUri.AbsoluteUri.Equals("https://inmemory.microsoft.com/common/discovery/keys"))
        {
            return await EmbeddedResourceReader.GetOpenIdConfigurationAsResponseMessage("microsoft-wellknown-keys.json");
        }

        throw new NotImplementedException();
    }
}

public class EmbeddedResourceReader
{
    private const string EmbeddedResourceQualifier = "Asos.DotNetCore.Auth.Api.ComponentTests";

    public static X509Certificate2 GetCertificate()
    {
        var resourceName = $"{EmbeddedResourceQualifier}.asos-test-api-auth-demo.pfx";
        using (var certificateStream = typeof(EmbeddedResourceReader).Assembly.GetManifestResourceStream(resourceName))
        {
            if (certificateStream == null)
            {
                return null;
            }

            var rawBytes = new byte[certificateStream.Length];
            for (var i = 0; i < certificateStream.Length; i++)
            {
                rawBytes[i] = (byte)certificateStream.ReadByte();
            }

            return new X509Certificate2(rawBytes, "api-demo", X509KeyStorageFlags.UserKeySet);
        }
    }

    public static async Task<HttpResponseMessage> GetOpenIdConfigurationAsResponseMessage(string resource)
    {
        var resourceName = $"{EmbeddedResourceQualifier}.well_known." + resource;
        using (var stream = typeof(EmbeddedResourceReader).Assembly.GetManifestResourceStream(resourceName))
        using (var reader = new StreamReader(stream))
        {
            var body = await reader.ReadToEndAsync();
            var content = new StringContent(body, Encoding.UTF8, "application/json");
            return new HttpResponseMessage()
            {
                Content = content,
            };
        }
    }
}