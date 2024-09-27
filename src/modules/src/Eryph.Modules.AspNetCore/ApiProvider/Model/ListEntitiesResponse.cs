using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Eryph.Modules.AspNetCore.ApiProvider.Model;

public class ListEntitiesResponse<T>
{
    [JsonPropertyName("value")]
    public required IReadOnlyList<T> Value { get; set; }
}
