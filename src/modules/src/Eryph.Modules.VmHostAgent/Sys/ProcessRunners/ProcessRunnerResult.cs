using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Eryph.Modules.VmHostAgent.Sys.ProcessRunners
{
    public readonly struct ProcessRunnerResult
    {
        public int ExitCode { get; init; }

        public string Output { get; init; }
    }
}
