using LanguageExt;
using LanguageExt.Effects.Traits;
using Spectre.Console;

namespace Eryph.AnsiConsole.Sys;

public interface HasAnsiConsole<RT> : HasCancel<RT>
    where RT : struct, HasAnsiConsole<RT>, HasCancel<RT>
{
    Eff<RT, AnsiConsoleIO> AnsiConsoleEff { get; }
}
