using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LanguageExt;

namespace Eryph.VmManagement.Sys;

public interface HasWmi<RT> where RT : struct, HasWmi<RT>
{
    Eff<RT, WmiIO> WmiEff { get; }
}
