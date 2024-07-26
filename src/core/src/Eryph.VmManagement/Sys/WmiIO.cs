using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Text;
using System.Threading.Tasks;
using LanguageExt;

using static LanguageExt.Prelude;

namespace Eryph.VmManagement.Sys;

public interface WmiIO
{
    public Seq<HashMap<string, Option<object>>> ExecuteQuery(string path, string query);
}

public class LiveWmiIO : WmiIO
{
    public static readonly WmiIO Default = new LiveWmiIO();

    public Seq<HashMap<string, Option<object>>> ExecuteQuery(string path, string query)
    {
        var scope = new ManagementScope(path);
        scope.Connect();

        var objectQuery = new ObjectQuery(query);
        using var searcher = new ManagementObjectSearcher(scope, objectQuery);
        using var collection = searcher.Get();

        // ToSeq() is lazy but we need to eagerly enumerate the collection
        // to avoid an ObjectDisposedException later. Hence, we call ToList().
        return collection.Cast<ManagementObject>()
            .ToList()
            .Map(mo => mo.Properties
                .Cast<PropertyData>()
                .Map(p => (p.Name, Optional(p.Value)))
                .ToHashMap())
            .ToSeq();
    }
}
