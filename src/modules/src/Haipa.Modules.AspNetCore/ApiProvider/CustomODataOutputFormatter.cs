using System.IO;
using System.Text;
using System.Threading.Tasks;
using Haipa.Modules.AspNetCore.ApiProvider.Model;
using Microsoft.AspNet.OData.Formatter;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OData;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace Haipa.Modules.AspNetCore.ApiProvider
{
    public class CustomODataOutputFormatter : ODataOutputFormatter
    {
        private readonly JsonSerializer _serializer;

        public CustomODataOutputFormatter()
            : base(new[] {ODataPayloadKind.Error})
        {
            _serializer = new JsonSerializer
            {
                Formatting = Formatting.None,
                NullValueHandling = NullValueHandling.Ignore,
                ContractResolver = new CamelCasePropertyNamesContractResolver()
            };

            SupportedMediaTypes.Add("application/json");
            SupportedEncodings.Add(new UTF8Encoding());
        }

        public override async Task WriteResponseBodyAsync(OutputFormatterWriteContext context,
            Encoding selectedEncoding)
        {
            if (!(context.Object is SerializableError serializableError))
            {
                await base.WriteResponseBodyAsync(context, selectedEncoding);
                return;
            }

            var env = context.HttpContext.RequestServices.GetRequiredService<IHostEnvironment>();

            var error = new ApiError(serializableError.CreateODataError(env.IsDevelopment()));

            var jObject = JObject.FromObject(error, _serializer);

            await using var writer = new StreamWriter(context.HttpContext.Response.Body);
            await jObject.WriteToAsync(new JsonTextWriter(writer));

            await writer.FlushAsync();
        }
    }
}