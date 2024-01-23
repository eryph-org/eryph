using Eryph.Modules.VmHostAgent.Networks;
using LanguageExt;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Eryph.Modules.VmHostAgent.Sys.ProcessRunners
{
    public static class ProcessRunner<RT>
        where RT : struct, HasProcessRunner<RT>
    {
        public static Aff<RT, ProcessRunnerResult> runProcess(
            string executablePath,
            string arguments,
            string workingDirectory = "",
            bool includeStandardError = false) =>
            default(RT).ProcessRunnerEff.MapAsync(e => e.RunProcess(
                executablePath, arguments, workingDirectory, includeStandardError));
    }
}
