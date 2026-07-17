using LanguageExt;

namespace Eryph.Core.Sys;

public interface HasProcessManager<RT>
    where RT : struct, HasProcessManager<RT>
{
    Eff<RT, ProcessManagerIO> ProcessManagerEff { get; }
}
