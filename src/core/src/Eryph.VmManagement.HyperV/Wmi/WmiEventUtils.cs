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
    public static Eff<WmiEvent> convertEvent(
        ManagementBaseObject wmiEvent,
        Seq<string> properties) =>
        from convertedEvent in convertObject(wmiEvent, Seq1("TIME_CREATED"))
        from creationTime in getCreated(convertedEvent)
        // Extract the TargetInstance manually for the WMI event
        from targetInstance in Eff(() => (ManagementBaseObject)wmiEvent["TargetInstance"])
            .MapFail(e => Error.New("The WMI event does not contain a valid TargetInstance.", e))
        from convertedTargetInstance in convertObject(targetInstance, properties)
        select new WmiEvent(creationTime, convertedTargetInstance);

    public static Eff<DateTimeOffset> getCreated(
        WmiObject wmiEvent) =>
        from value in getRequiredValue<ulong>(
            wmiEvent, "TIME_CREATED")
        from creationTime in Eff(() => DateTimeOffset.FromFileTime((long)value))
            .MapFail(e => Error.New("The creation time of the event is invalid.", e))
        select creationTime;
}
