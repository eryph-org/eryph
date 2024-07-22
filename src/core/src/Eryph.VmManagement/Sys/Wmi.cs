using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Text;
using System.Threading.Tasks;
using LanguageExt;

namespace Eryph.VmManagement.Sys;

public static class Wmi<RT> where RT: struct, HasWmi<RT>
{
    public static Eff<RT, ManagementScope> createScope(string path) =>
        default(RT).WmiEff.Map(wmi => wmi.CreateScope(path));

    public static Eff<RT, Seq<ManagementObject>> executeQuery(ManagementScope scope, string query) =>
        default(RT).WmiEff.Map(wmi => wmi.ExecuteQuery(scope, query));
}
