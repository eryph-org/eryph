using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Eryph.ConfigModel;

namespace Eryph.Rebus;

public class EryphJsonSerializerOptions
{
    public static JsonSerializerOptions Default => new(JsonSerializerDefaults.Web)
    {
        Converters = { new EryphNameJsonConverter() },
        
        // This enables serialization support for ValueTuples by
        // including public fields in the serialization.
        IncludeFields = true,
    };
}
