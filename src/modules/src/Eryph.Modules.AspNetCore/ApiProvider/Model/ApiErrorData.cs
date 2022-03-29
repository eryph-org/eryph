using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using JetBrains.Annotations;

namespace Eryph.Modules.AspNetCore.ApiProvider.Model
{
    public class ApiErrorData : ApiErrorBody
    {
        [JsonPropertyName("details")] public List<ApiErrorBody>? Details { get; set; }
        [JsonPropertyName("innererror")] public InnerErrorData? InnerError { get; set; }

        public class InnerErrorData
        {
            [Newtonsoft.Json.JsonExtensionData] public IDictionary<string, JsonElement>? AdditionalData { [UsedImplicitly] get; set; }
        }
    }
}