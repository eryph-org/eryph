using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Runtime.Versioning;
using System.Text;
using System.Threading.Tasks;
using Eryph.VmManagement.Wmi;
using LanguageExt;

using static LanguageExt.Prelude;

namespace Eryph.VmManagement.Sys;

public interface WmiIO
{
    public Fin<Seq<WmiObject>> ExecuteQuery(
        string scope,
        Seq<string> properties,
        string className,
        Option<string> whereClause);
}

[SupportedOSPlatform("windows")]
public readonly struct LiveWmiIO : WmiIO
{
    public static readonly WmiIO Default = new LiveWmiIO();

    public Fin<Seq<WmiObject>> ExecuteQuery(
        string scope,
        Seq<string> properties,
        string className,
        Option<string> whereClause)
    {
        using var searcher = new ManagementObjectSearcher(
            new ManagementScope(scope),
            new ObjectQuery($"SELECT {string.Join(", ", properties)} "
                + $"FROM {className}"
                + whereClause.Map(c => $" WHERE {c}").IfNone("")));
        using var collection = searcher.Get();
        var managementObjects = collection.Cast<ManagementBaseObject>().ToSeq();
        try
        {
            return ConvertObjects(managementObjects, properties).Run();
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

    private static Eff<Seq<WmiObject>> ConvertObjects(
        Seq<ManagementBaseObject> managementObjects,
        Seq<string> properties) =>
        managementObjects
            .Map(o => WmiUtils.convertObject(o, properties))
            .Sequence();
}
