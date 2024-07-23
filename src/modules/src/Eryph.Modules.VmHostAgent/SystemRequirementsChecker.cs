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
        from features in retry(
            ComputeSchedule(isService),
            from __1 in logInformation("Checking if Hyper-V is installed...")
            from features in isHyperVInstalled()
            select features)
        from _1 in guard(features.IsPlatformInstalled,
            Error.New("Hyper-V platform (Microsoft-Hyper-V) is not installed."))
        from _2 in guard(features.IsPowershellInstalled,
            Error.New("Hyper-V powershell module (Microsoft-Hyper-V-Management-PowerShell) is not installed."))
        // Even with our service depending on the Hyper-V management service (VMMS),
        // the Hyper-V WMI namespace is sometimes not responding during system startup.
        // Hence, we try a couple of times to query it.
        from _3 in retry(
            ComputeSchedule(isService),
            from __1 in logInformation("Checking if Hyper-V is available...")
            from __2 in WmiQueries<RT>.getHyperVDefaultPaths().ToAff()
            select unit)
        select unit;

    private static Aff<RT, (bool IsPlatformInstalled, bool IsPowershellInstalled)> isHyperVInstalled() =>
        from features in WmiQueries<RT>.getFeatures()
        from platformFeature in features
            .Find(f => f.Name == "Microsoft-Hyper-V")
            .ToEff(Error.New("Could not query the install state of the feature 'Microsoft-Hyper-V'."))
        from powershellFeature in features
            .Find(f => f.Name == "Microsoft-Hyper-V-Management-PowerShell")
            .ToEff(Error.New("Could not query the install state of the feature 'Microsoft-Hyper-V-Management-PowerShell'."))
        select (platformFeature.IsInstalled, powershellFeature.IsInstalled);

    private static Schedule ComputeSchedule(bool isService) =>
        isService
            ? Schedule.NoDelayOnFirst
              & Schedule.linear(TimeSpan.FromSeconds(1))
              & Schedule.maxDelay(TimeSpan.FromSeconds(10))
              & Schedule.upto(TimeSpan.FromMinutes(1))
            : Schedule.Never;

    private static Eff<RT, Unit> logInformation(string msg) =>
        Logger<RT>.logInformation(nameof(SystemRequirementsChecker<RT>), msg);
}
