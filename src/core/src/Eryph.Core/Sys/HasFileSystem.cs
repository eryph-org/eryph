using LanguageExt.Effects.Traits;
using LanguageExt;

namespace Eryph.Core.Sys;

public interface HasFileSystem<RT> : HasCancel<RT>
    where RT : struct, HasFileSystem<RT>, HasCancel<RT>
{
    Eff<RT, FileSystemIO> FileSystemEff { get; }
}
