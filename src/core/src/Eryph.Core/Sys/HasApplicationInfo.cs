using LanguageExt;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Eryph.Core.Sys;

public interface HasApplicationInfo<RT> where RT : struct, HasApplicationInfo<RT>
{
    Eff<RT, IApplicationInfoProvider> ApplicationInfoProviderEff { get; }
}
