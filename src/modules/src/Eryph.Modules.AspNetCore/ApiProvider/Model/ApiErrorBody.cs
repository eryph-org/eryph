using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Eryph.Modules.AspNetCore.ApiProvider.Model
{
    public class ApiErrorBody
    {
        [Required] [JsonProperty("code")] public string? Code { get; set; } = "";

        [Required] [JsonProperty("message")] public string? Message { get; set; } = "";

        [JsonProperty("target")] public string? Target { get; set; }

        [JsonExtensionData] [UsedImplicitly] public IDictionary<string, JToken>? AdditionalData { get; set; }
    }
}