using System.Collections.Generic;
using Newtonsoft.Json;

namespace Haipa.Modules.AspNetCore.ApiProvider.Model
{
    public class ListResponse<T>
    {
        [JsonProperty("count")] public string? Count { get; set; }


        [JsonProperty("nextLink")] public string? NextLink { get; set; }

        /// <summary>
        /// Gets or sets the OData response content in the "value".
        /// </summary>
        /// <value>The response content within "value".</value>
        [JsonProperty("value")]
        public IEnumerable<T> Value { get; set; } = default!;
    }
}