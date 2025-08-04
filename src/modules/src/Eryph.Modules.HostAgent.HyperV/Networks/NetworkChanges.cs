using LanguageExt;
using LanguageExt.Effects.Traits;

namespace Eryph.Modules.HostAgent.Networks;

public record  NetworkChanges<RT> where RT : struct, HasCancel<RT>
{
    public Seq<NetworkChangeOperation<RT>> Operations { get; init; }

}