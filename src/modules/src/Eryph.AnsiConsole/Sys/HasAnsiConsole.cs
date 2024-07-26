using LanguageExt;
using LanguageExt.Effects.Traits;

namespace Eryph.AnsiConsole.Sys;

public interface HasAnsiConsole<RT> : HasCancel<RT>
    where RT : struct, HasAnsiConsole<RT>, HasCancel<RT>
{
    Eff<RT, AnsiConsoleIO> AnsiConsoleEff { get; }
}
