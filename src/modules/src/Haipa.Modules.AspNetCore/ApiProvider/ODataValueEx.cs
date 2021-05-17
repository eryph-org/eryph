using Microsoft.AspNet.OData;
using Newtonsoft.Json;

namespace Haipa.Modules.AspNetCore.ApiProvider
{
    public class ODataValueEx<T> : ODataValue<T>
    {
        [JsonProperty("@odata.context")] public string? ODataContext { get; set; }

        [JsonProperty("@odata.count")] public int ODataCount { get; set; }

        [JsonProperty("@odata.nextLink")] public string? ODataNextLink { get; set; }
    }
}