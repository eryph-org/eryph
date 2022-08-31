using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;
using JetBrains.Annotations;

namespace Eryph.Modules.AspNetCore.ApiProvider.Model
{
    public class ApiErrorBody
    {
        [Required] [JsonPropertyName("code")] public string? Code { get; set; } = "";

        [Required] [JsonPropertyName("message")] public string? Message { get; set; } = "";

        [JsonPropertyName("target")] public string? Target { get; set; }

        [JsonExtensionData] [UsedImplicitly] public IDictionary<string, JsonElement>? AdditionalData { get; set; }
    }
}