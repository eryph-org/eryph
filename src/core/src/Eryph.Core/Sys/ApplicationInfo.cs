using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LanguageExt;

namespace Eryph.Core.Sys;

public class ApplicationInfo<RT>
    where RT : struct, HasApplicationInfo<RT>
{
    public static Eff<RT, string> applicationId() =>
        default(RT).ApplicationInfoProviderEff.Map(p => p.ApplicationId);
}
