using System;
using Eryph.Modules.VmHostAgent.Networks;
using Eryph.VmManagement.Sys;
using LanguageExt;
using LanguageExt.Common;
using LanguageExt.Effects.Traits;
using static LanguageExt.Prelude;

namespace Eryph.Modules.VmHostAgent;

public static class SystemRequirementsChecker<RT> where RT : struct,
    HasCancel<RT>,
    HasLogger<RT>,
    HasWmi<RT>
{
    public static Aff<RT, Unit> ensureHyperV(bool isService) =>
        // Check if the necessary Hyper-V features are installed.
        // When running as a service, we retry the check a few times
        // in case WMI is not responding yet during system startup.
        from _1 in retryWhile(
            ComputeSchedule(isService),
            from __1 in logInformation("Checking if Hyper-V is installed...")
            from __2 in ensureHyperVFeatures()
            select unit,
            e => e.IsExceptional)
        // Even with our service depending on the Hyper-V management service (VMMS),
        // the Hyper-V WMI namespace is sometimes not responding during system startup.
        // Hence, we try a couple of times to query it.
        from _2 in retry(
            ComputeSchedule(isService),
            from __1 in logInformation("Checking if Hyper-V is available...")
            from __2 in WmiQueries<RT>.getHyperVDefaultPaths().ToAff()
            select unit)
        select unit;

    private static Aff<RT, Unit> ensureHyperVFeatures() =>
        from features in WmiQueries<RT>.getFeatures()
        from _ in features
            .Find(f => f.Name == "Microsoft-Hyper-V")
            .Filter(f => f.IsInstalled)
            .ToEff(Error.New("Hyper-V platform (Microsoft-Hyper-V) is not installed."))
        from __ in features
            .Find(f => f.Name == "Microsoft-Hyper-V-Management-PowerShell")
            .Filter(f => f.IsInstalled)
            .ToEff(Error.New("Hyper-V PowerShell module (Microsoft-Hyper-V-Management-PowerShell) is not installed."))
        select unit;

    private static Schedule ComputeSchedule(bool isService) =>
        isService
            ? Schedule.NoDelayOnFirst
              & Schedule.linear(TimeSpan.FromSeconds(1))
              & Schedule.maxDelay(TimeSpan.FromSeconds(10))
              & Schedule.upto(TimeSpan.FromMinutes(1))
            : Schedule.Once;

    private static Eff<RT, Unit> logInformation(string msg) =>
        Logger<RT>.logInformation(nameof(SystemRequirementsChecker<RT>), msg);
}
