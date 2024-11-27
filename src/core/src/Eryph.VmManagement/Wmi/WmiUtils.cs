using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Text;
using System.Threading.Tasks;
using LanguageExt;
using LanguageExt.Common;

using static LanguageExt.Prelude;

namespace Eryph.VmManagement.Wmi;

public static class WmiUtils
{
    public static Eff<T> GetPropertyValue<T>(
        ManagementBaseObject mo,
        string propertyName) =>
        from property in FindProperty(mo, propertyName)
        from value in Optional(property.Value)
            .ToEff(Error.New($"The property '{propertyName} is null."))
        from convertedValue in ConvertValue<T>(propertyName, value)
        select convertedValue;

    public static Eff<Option<T>> GetOptionalPropertyValue<T>(
        ManagementBaseObject mo,
        string propertyName) =>
        from property in FindProperty(mo, propertyName)
        from convertedValue in Optional(property.Value)
            .Map(v => ConvertValue<T>(propertyName, v))
            .Sequence()
        select convertedValue;

    private static Eff<PropertyData> FindProperty(
        ManagementBaseObject managementObject,
        string propertyName) =>
        from properties in Eff(() => managementObject.Properties.Cast<PropertyData>().ToSeq())
        from property in properties.Find(p => p.Name == propertyName)
            .ToEff(Error.New($"The property '{propertyName}' does not exist in the WMI object."))
        select property;

    private static Eff<T> ConvertValue<T>(
        string propertyName,
        object value) =>
        from _ in unitEff
        let result = typeof(T).IsEnum
            ? ConvertToEnum<T>(value)
            : Eff(() => (T)value).MapFail(_ => Error.New($"The value '{value}' is not of type {nameof(T)}."))
        from convertedValue in result
            .MapFail(e => Error.New($"The value '{value}' of property '{propertyName}' is invalid."))
        select convertedValue;

    private static Eff<T> ConvertToEnum<T>(
        object value) =>
        from _ in guard(typeof(T).IsEnum,
                Error.New("Cannot convert value to a type which is not an enum."))
            .ToEff()
        from enumValue in value switch
        {
            string s => Eff(() => (T)Enum.Parse(typeof(T), s, true))
                .MapFail(_ => Error.New($"The value '{s}' is not valid for {nameof(T)}.")),
            { } v when Enum.IsDefined(typeof(T), v) => Eff(() => (T)Enum.ToObject(typeof(T), v))
                .MapFail(_ => Error.New($"The value '{s}' is not valid for {nameof(T)}.")),
            _ => FailEff<T>(Error.New($"The value '{s}' is not valid for {nameof(T)}."))
        }
        select enumValue;
}
