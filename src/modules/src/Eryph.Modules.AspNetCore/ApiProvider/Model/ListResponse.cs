using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Eryph.Modules.AspNetCore.ApiProvider.Model
{
    public class ListResponse<T>
    {
        [JsonPropertyName("count")] public string? Count { get; set; }


        [JsonPropertyName("nextLink")] public string? NextLink { get; set; }

        /// <summary>
        /// Gets or sets the OData response content in the "value".
        /// </summary>
        /// <value>The response content within "value".</value>
        [JsonPropertyName("value")]
        public IEnumerable<T> Value { get; set; } = default!;
    }
}