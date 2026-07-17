using System;
using LanguageExt;

namespace Eryph.Core.Sys;

public static class ProcessManager<RT> where RT : struct, HasProcessManager<RT>
{
    public static Eff<RT, Option<string>> getProcessName(int processId) =>
        default(RT).ProcessManagerEff.Map(m => m.GetProcessName(processId));

    public static Eff<RT, bool> stopProcess(int processId, TimeSpan timeout) =>
        default(RT).ProcessManagerEff.Map(m => m.StopProcess(processId, timeout));
}
