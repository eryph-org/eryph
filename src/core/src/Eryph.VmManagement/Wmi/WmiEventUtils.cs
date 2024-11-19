using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Text;
using System.Threading.Tasks;
using LanguageExt;
using LanguageExt.Common;

using static LanguageExt.Prelude;
using static Eryph.VmManagement.Wmi.WmiUtils;

namespace Eryph.VmManagement.Wmi;

public static class WmiEventUtils
{ 
    public static Eff<ManagementBaseObject> GetTargetInstance(
        ManagementBaseObject wmiEvent) =>
        from targetInstance in GetPropertyValue<ManagementBaseObject>(
            wmiEvent, "TargetInstance")
        select targetInstance;

    public static Eff<DateTimeOffset> GetCreationTime(
        ManagementBaseObject wmiEvent) =>
        from value in GetPropertyValue<ulong>(
            wmiEvent, "TIME_CREATED")
        from creationTime in Eff(() => DateTimeOffset.FromFileTime((long)value))
            .MapFail(e => Error.New("The creation time of the event is invalid.", e))
        select creationTime;
}
