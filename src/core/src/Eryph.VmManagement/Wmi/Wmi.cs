using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Text;
using System.Threading.Tasks;
using Eryph.VmManagement.Sys;
using LanguageExt;

namespace Eryph.VmManagement.Wmi;

public static class Wmi<RT> where RT: struct, HasWmi<RT>
{
    public static Eff<RT, Seq<WmiObject>> executeQuery(
        string scope,
        Seq<string> properties,
        string className,
        Option<string> whereClause) =>
        default(RT).WmiEff.Bind(wmi => wmi.ExecuteQuery(scope, properties, className, whereClause).ToEff());
}
