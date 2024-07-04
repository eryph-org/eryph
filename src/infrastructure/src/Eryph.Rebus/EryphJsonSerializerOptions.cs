using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Eryph.Rebus;

public class EryphJsonSerializerOptions
{
    public static JsonSerializerOptions Default => new()
    {
        Converters =
        {
            new EryphNameJsonConverter(),
        },
    };
}