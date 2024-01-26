using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Eryph.Modules.VmHostAgent.Sys.ProcessRunners
{
    public class ProcessRunnerResult
    {
        public ProcessRunnerResult(int exitCode, string output)
        {
            ExitCode = exitCode;
            Output = output;
        }

        public int ExitCode { get; init; }

        public string Output { get; init; }
    }
}
