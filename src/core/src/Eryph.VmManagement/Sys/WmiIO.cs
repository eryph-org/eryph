using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Text;
using System.Threading.Tasks;
using LanguageExt;
using Microsoft.Management.Infrastructure;

namespace Eryph.VmManagement.Sys;

public interface WmiIO
{
    public ManagementScope CreateScope(string path);

    public Seq<ManagementObject> ExecuteQuery(ManagementScope scope, string query);
}

public class LiveWmiIO : WmiIO
{
    public static readonly WmiIO Default = new LiveWmiIO();

    public ManagementScope CreateScope(string path)
    {
        var scope = new ManagementScope(path);
        scope.Connect();
        return scope;
    }

    public Seq<ManagementObject> ExecuteQuery(ManagementScope scope, string query)
    {
        var objectQuery = new ObjectQuery(query);
        using var searcher = new ManagementObjectSearcher(scope, objectQuery);
        using var collection = searcher.Get();

        // ToSeq() is lazy but we need to eagerly enumerate the collection
        // to avoid an ObjectDisposedException later. Hence, we call ToList().
        return collection.Cast<ManagementObject>().ToList().ToSeq();
    }
}