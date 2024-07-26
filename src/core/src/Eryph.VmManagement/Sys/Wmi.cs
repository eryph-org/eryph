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
    public static Eff<RT, Seq<HashMap<string, Option<object>>>> executeQuery(
        string path, string query) =>
        default(RT).WmiEff.Map(wmi => wmi.ExecuteQuery(path, query));
}
