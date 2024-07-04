using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Dbosoft.Functional.DataTypes;
using Eryph.ConfigModel;
using LanguageExt;
using LanguageExt.TypeClasses;

namespace Eryph.Rebus;

public class EryphNameJsonConverter : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert)
    {
        return typeToConvert.BaseType is not null
            && typeToConvert.BaseType.IsGenericType
            && typeToConvert.BaseType.GetGenericTypeDefinition() == typeof(EryphName<>);
    }

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        if (!CanConvert(typeToConvert))
            throw new ArgumentException("The given type is not supported", nameof(typeToConvert));

        return (JsonConverter)Activator.CreateInstance(
            typeof(EryphNameJsonConverter<>).MakeGenericType(typeToConvert));
    }
}

public class EryphNameJsonConverter<T> : JsonConverter<T> where T : EryphName<T>
{
    public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return (T)Activator.CreateInstance(typeToConvert, reader.GetString());
    }

    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.Value);
    }
}
