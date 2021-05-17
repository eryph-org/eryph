using System.Net;
using Haipa.Modules.AspNetCore.ApiProvider.Model;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Haipa.Modules.AspNetCore.ApiProvider
{
    public class ApiExceptionFilter : ExceptionFilterAttribute
    {
        public static readonly JsonSerializerSettings ODataErrorJsonSerializerSettings = new JsonSerializerSettings
        {
            Formatting = Formatting.None,
            NullValueHandling = NullValueHandling.Ignore,
            ContractResolver = new CamelCasePropertyNamesContractResolver()
        };

        public override void OnException(ExceptionContext context)
        {
            var env = context.HttpContext.RequestServices.GetRequiredService<IHostEnvironment>();

            var response = new ApiError(context.Exception.CreateODataError(env.IsDevelopment()));
            context.Result = new ContentResult
            {
                Content = JsonConvert.SerializeObject(response, ODataErrorJsonSerializerSettings),
                ContentType = "application/json", StatusCode =
                    (int) HttpStatusCode.InternalServerError
            };

            base.OnException(context);
        }
    }
}