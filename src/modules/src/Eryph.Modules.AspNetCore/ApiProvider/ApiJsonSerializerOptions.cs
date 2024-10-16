using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Eryph.Modules.AspNetCore.ApiProvider;

public static class ApiJsonSerializerOptions
{
    public static JsonSerializerOptions Options =>
        new JsonSerializerOptions(JsonSerializerDefaults.Web)
            .AddEryphApiSettings();

    public static JsonSerializerOptions AddEryphApiSettings(
        this JsonSerializerOptions options)
    {
        if (!options.Converters.OfType<JsonStringEnumConverter>().Any())
        {
            options.Converters.Add(new JsonStringEnumConverter());
        }

        options.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        options.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;

        return options;
    }
}
