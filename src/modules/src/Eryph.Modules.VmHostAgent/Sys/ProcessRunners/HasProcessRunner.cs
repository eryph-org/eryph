using LanguageExt.Effects.Traits;
using LanguageExt;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Eryph.Modules.VmHostAgent.Sys.ProcessRunners
{
    public interface HasProcessRunner<RT> : HasCancel<RT>
        where RT : struct, HasProcessRunner<RT>, HasCancel<RT>
    {
        Eff<RT, ProcessRunnerIO> ProcessRunnerEff { get; }
    }
}
