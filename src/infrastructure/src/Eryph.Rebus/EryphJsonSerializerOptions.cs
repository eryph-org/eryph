using System.Text.Json;
using Dbosoft.Functional.Json.DataTypes;

namespace Eryph.Rebus;

public static class EryphJsonSerializerOptions
{
    public static JsonSerializerOptions Options => new(JsonSerializerDefaults.Web)
    {
        Converters = { new NewTypeJsonConverter() },

        // This enables serialization support for ValueTuples by
        // including public fields in the serialization.
        IncludeFields = true,
    };
}
