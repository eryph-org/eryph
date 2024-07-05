using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Eryph.ConfigModel;

namespace Eryph.Rebus;

public class EryphNameJsonConverter : JsonConverterFactory
{
    /// <inheritdoc/>
    /// <remarks>
    /// The logic only supports direct subclasses of <see cref="EryphName{T}"/>.
    /// Making the logic more generic is complicated as <see cref="LanguageExt.NewType{NEWTYPE,A}"/>
    /// uses complex generics.
    /// </remarks>>
    public override bool CanConvert(Type typeToConvert) =>
        typeToConvert.BaseType is not null
           && typeToConvert.BaseType.IsGenericType
           && typeToConvert.BaseType.GetGenericTypeDefinition() == typeof(EryphName<>);

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options) =>
        (JsonConverter)Activator.CreateInstance(
            typeof(EryphNameJsonConverter<>).MakeGenericType(typeToConvert));
    
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
