using Eryph.VmManagement.Networking.Settings;
using LanguageExt;
using LanguageExt.Common;

namespace Eryph.Core;

public interface INetworkProviderManager
{
    EitherAsync<Error, string> GetCurrentConfigurationYaml();
    EitherAsync<Error, NetworkProvidersConfiguration> GetCurrentConfiguration();
    Either<Error, NetworkProvidersConfiguration> ParseConfigurationYaml(string yaml);
    EitherAsync<Error, Unit> SaveConfigurationYaml(string config);
}