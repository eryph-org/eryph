using System;
using System.Threading;
using Eryph.Core;
using Eryph.Core.Network;
using Eryph.Messages.Components;
using Eryph.ModuleCore.Configuration;
using LanguageExt;
using LanguageExt.Common;
using SimpleInjector;
using SimpleInjector.Lifestyles;
using static LanguageExt.Prelude;

namespace Eryph.Modules.Controller.Components;

/// <summary>
/// Decorates the host's file-backed <see cref="INetworkProviderManager"/> so the controller's own
/// network realization and the distributed payload both read the operator-authored NetworkProviders
/// value when one exists (management API), falling back to the local <c>p_networks.yml</c> otherwise.
/// This is the single seam that keeps every controller-side consumer and the config source in step, so
/// authoring cannot make the controller diverge from what agents receive.
/// </summary>
/// <remarks>
/// Only the READ path is overlaid. WRITES go to the inner file manager: the authored store is written
/// solely by the authoring command, so the IP-pool cursor write-back (which persists runtime allocation
/// state) does not append authored versions on every allocation. In eryph-zero there is no management
/// API, so nothing is authored and this transparently returns the file value.
/// </remarks>
internal sealed class AuthoredNetworkProviderManager(
    INetworkProviderManager inner,
    Container container)
    : INetworkProviderManager
{
    public NetworkProviderDefaults Defaults => inner.Defaults;

    public EitherAsync<Error, string> GetCurrentConfigurationYaml() =>
        from authored in GetAuthoredPayload()
        from yaml in authored.Match(
            Some: RightAsync<Error, string>,
            None: inner.GetCurrentConfigurationYaml)
        select yaml;

    public EitherAsync<Error, NetworkProvidersConfiguration> GetCurrentConfiguration() =>
        from authored in GetAuthoredPayload()
        from config in authored.Match(
            Some: Deserialize,
            None: inner.GetCurrentConfiguration)
        select config;

    // Writes always target the local file; the authored store is written only via the authoring command.
    public EitherAsync<Error, Unit> SaveConfigurationYaml(string config) =>
        inner.SaveConfigurationYaml(config);

    public EitherAsync<Error, Unit> SaveConfiguration(NetworkProvidersConfiguration config) =>
        inner.SaveConfiguration(config);

    private EitherAsync<Error, Option<string>> GetAuthoredPayload() =>
        TryAsync(async () =>
        {
            // The authored store is scoped, so resolve it in a dedicated scope — this decorator wraps a
            // singleton and may be used outside a request scope (mirrors the config sources).
            await using var scope = AsyncScopedLifestyle.BeginScope(container);
            var authored = await scope.GetInstance<IAuthoredConfigStore>()
                .GetCurrentAsync(ConfigDomain.NetworkProviders, ConfigScope.Default, CancellationToken.None);
            return Optional(authored?.Payload);
        }).ToEither(ex => Error.New(ex));

    private static EitherAsync<Error, NetworkProvidersConfiguration> Deserialize(string yaml) =>
        Try(() => NetworkProvidersConfigYamlSerializer.Deserialize(yaml))
            .ToEither(ex => Error.New(ex))
            .ToAsync();
}
