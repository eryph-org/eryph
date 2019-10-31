using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace Haipa.TestUtils.Handlers
{
    public class TestServerHandler : DelegatingHandler
    {
        public TestServerHandler(HttpMessageHandler innerHandler) : base(innerHandler)
        {
            
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", "dGVzdDp0ZXN0");
            return base.SendAsync(request, cancellationToken);
        }
    }
}