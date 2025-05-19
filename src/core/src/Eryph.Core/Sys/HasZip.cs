using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LanguageExt;
using LanguageExt.Effects.Traits;

namespace Eryph.Core.Sys;

public interface HasZip<RT> : HasCancel<RT>
    where RT : struct, HasZip<RT>, HasCancel<RT>
{
    Eff<RT, ZipIO> ZipEff { get; }
}
