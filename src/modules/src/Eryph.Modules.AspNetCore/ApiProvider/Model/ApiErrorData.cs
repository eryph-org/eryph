using System.Collections.Generic;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Eryph.Modules.AspNetCore.ApiProvider.Model
{
    public class ApiErrorData : ApiErrorBody
    {
        [JsonProperty("details")] public List<ApiErrorBody>? Details { get; set; }
        [JsonProperty("innererror")] public InnerErrorData? InnerError { get; set; }

        public class InnerErrorData
        {
            [JsonExtensionData] public IDictionary<string, JToken>? AdditionalData { [UsedImplicitly] get; set; }
        }
    }
}