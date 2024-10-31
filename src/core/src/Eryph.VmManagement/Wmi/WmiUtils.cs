using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Text;
using System.Threading.Tasks;
using JetBrains.Annotations;
using LanguageExt;
using LanguageExt.Common;

using static LanguageExt.Prelude;

namespace Eryph.VmManagement.Wmi;

public static class WmiUtils
{
    public static HashMap<string, Option<object>> GetProperties(
        ManagementBaseObject mo) =>
        mo.Properties.Cast<PropertyData>()
            .Map(p => (p.Name, Optional(p.Value)))
            .ToHashMap();
}
