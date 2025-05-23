﻿using System;
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
    /// <summary>
    /// Converts the given <paramref name="managementObject"/> to
    /// our own <see cref="WmiObject"/> representation. Missing
    /// properties (present in <paramref name="properties"/> but not
    /// in the <paramref name="managementObject"/>) are ignored.
    /// </summary>
    public static Eff<WmiObject> convertObject(
        ManagementBaseObject managementObject,
        Seq<string> properties) =>
        from propertyData in properties
            .Map(p => findPropertyData(managementObject, p))
            .Sequence()
        let validPropertyData = propertyData.Somes()
        from values in validPropertyData
            .Map(convertProperty)
            .Sequence()
        select new WmiObject(values.ToHashMap());

    private static Eff<(string, Option<object>)> convertProperty(
        PropertyData propertyData) =>
        // Reject nested WMI objects. We do not want to leak WMI objects
        // as we expect the caller to dispose them immediately after
        // the conversion.
        from _ in guardnot(propertyData.Type == CimType.Object,
                Error.New($"The property '{propertyData.Name}' contains a WMI object."))
            .ToEff()
        select (propertyData.Name, Optional(propertyData.Value));

    public static Eff<T> getRequiredValue<T>(
        WmiObject mo,
        string propertyName) =>
        from optionalValue in findProperty(mo, propertyName)
        from value in optionalValue
            .ToEff(Error.New($"The property '{propertyName} is null."))
        from convertedValue in convertValue<T>(propertyName, value)
        select convertedValue;

    public static Eff<Option<T>> getValue<T>(
        WmiObject wmiObject,
        string propertyName) =>
        from optionalValue in findProperty(wmiObject, propertyName)
        from convertedValue in optionalValue
            .Map(v => convertValue<T>(propertyName, v))
            .Sequence()
        select convertedValue;

    private static Eff<Option<PropertyData>> findPropertyData(
        ManagementBaseObject managementObject,
        string propertyName) =>
        from _ in SuccessEff(unit)
        let propertyDataCollection = propertyName.StartsWith("__")
            ? managementObject.SystemProperties
            : managementObject.Properties
        from properties in Eff(() => propertyDataCollection.Cast<PropertyData>().ToSeq())
        let property = properties.Find(p => p.Name == propertyName)
        select property;

    private static Eff<Option<object>> findProperty(
        WmiObject wmiObject,
        string propertyName) =>
        from property in wmiObject.Properties.Find(propertyName)
            .ToEff(Error.New($"The property '{propertyName}' does not exist in the WMI object."))
        select property;

    private static Eff<T> convertValue<T>(
        string propertyName,
        object value) =>
        from _ in unitEff
        let result = typeof(T).IsEnum switch
        {
            true => value switch
            {
                string s => Eff(() => (T)Enum.Parse(typeof(T), s, true))
                    .MapFail(_ => Error.New($"The value '{value}' is not valid for {typeof(T).Name}.")),
                { } v when Enum.IsDefined(typeof(T), v) => Eff(() => (T)Enum.ToObject(typeof(T), v))
                    .MapFail(_ => Error.New($"The value '{value}' is not valid for {typeof(T).Name}.")),
                _ => FailEff<T>(Error.New($"The value '{value}' is not valid for {typeof(T).Name}."))
            },
            false => value is T
                ? Eff(() => (T)value)
                : FailEff<T>(Error.New($"The value '{value}' is not of type {typeof(T).Name}."))
        }
        from convertedValue in result
            .MapFail(e => Error.New($"The value '{value}' of property '{propertyName}' is invalid.", e))
        select convertedValue;
}
