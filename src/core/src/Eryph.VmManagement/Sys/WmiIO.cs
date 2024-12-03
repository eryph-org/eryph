using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Text;
using System.Threading.Tasks;
using LanguageExt;
using LanguageExt.Common;
using static LanguageExt.Prelude;

namespace Eryph.VmManagement.Sys;

public interface WmiIO
{
    public Eff<Seq<HashMap<string, Option<object>>>> ExecuteQuery(
        string scope,
        Seq<string> properties,
        string className,
        Option<string> whereClause);
}

public readonly struct LiveWmiIO : WmiIO
{
    public static readonly WmiIO Default = new LiveWmiIO();

    public Eff<Seq<HashMap<string, Option<object>>>> ExecuteQuery(
        string scope,
        Seq<string> properties,
        string className,
        Option<string> whereClause)
    {
        using var searcher = new ManagementObjectSearcher(
            new ManagementScope(scope),
            new ObjectQuery($"SELECT {string.Join(',', properties)} "
                + $"FROM {className}"
                + whereClause.Map(c => $" WHERE {c}").IfNone("")));
        using var collection = searcher.Get();
        var managementObjects= collection.Cast<ManagementBaseObject>().ToList();
        try
        {
            return ConvertObjects(managementObjects.ToSeq(), properties);
        }
        finally
        {
            // The ManagementBaseObjects must be explicitly disposed as they
            // hold COM objects. Furthermore, ManagementBaseObject.Dispose()
            // does only work correctly when being invoked directly.
            // The method is defined with the new keyword and will not be invoked
            // via the IDisposable interface (e.g. with a using statement).
            foreach (var managementObject in managementObjects)
            {
                managementObject.Dispose();
            }
        }
    }

    private static Eff<Seq<HashMap<string, Option<object>>>> ConvertObjects(
        Seq<ManagementBaseObject> managementObjects,
        Seq<string> properties) =>
        managementObjects
            .Map(o => ConvertObject(o, properties))
            .Sequence();

    private static Eff<HashMap<string, Option<object>>> ConvertObject(
        ManagementBaseObject managementObject,
        Seq<string> properties) =>
        from values in properties
            .Map(p => from value in ConvertProperty(managementObject, p)
                      select (p, value))
            .Sequence()
        select values.ToHashMap();

    private static Eff<Option<object>> ConvertProperty(
        ManagementBaseObject managementObject,
        string property) =>
        from _ in SuccessEff(unit)
        let propertyDataCollection = property.StartsWith("__")
            ? managementObject.SystemProperties
            : managementObject.Properties
        from propertyData in propertyDataCollection
            .Cast<PropertyData>()
            .Find(p => p.Name == property)
            .ToEff(Error.New($"The property '{property}' was not found."))
        from __ in guardnot(propertyData.Type == CimType.Object,
            Error.New($"The property '{property}' contains a WMI object. These are not supported by this query engine."))
        select Optional(propertyData.Value);
}
