using LanguageExt;
using LanguageExt.Common;

namespace Eryph.Core.VmAgent;

public interface IHostSettingsProvider
{
    EitherAsync<Error, HostSettings> GetHostSettings();
}