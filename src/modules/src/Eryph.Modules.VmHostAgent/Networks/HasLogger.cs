using LanguageExt;
using Microsoft.Extensions.Logging;

namespace Eryph.Modules.VmHostAgent.Networks;

public interface HasLogger<RT>
    where RT : struct, HasLogger<RT>
{
    Eff<RT, ILogger> Logger(string category);
    Eff<RT, ILogger<T>> Logger<T>();


}