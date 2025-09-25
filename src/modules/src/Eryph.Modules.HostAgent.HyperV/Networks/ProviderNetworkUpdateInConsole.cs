using System;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading;
using Eryph.AnsiConsole.Sys;
using Eryph.Core.Network;
using Eryph.Modules.HostAgent.Networks.OVS;
using Eryph.VmManagement.Sys;
using LanguageExt;
using LanguageExt.Common;
using LanguageExt.Effects.Traits;
using Spectre.Console;

using static LanguageExt.Prelude;
using static Eryph.AnsiConsole.Prelude;

namespace Eryph.Modules.HostAgent.Networks;

public static class ProviderNetworkUpdateInConsole<RT>
    where RT : struct,
    HasAnsiConsole<RT>,
    HasCancel<RT>,
    HasOVSControl<RT>,
    HasAgentSyncClient<RT>,
    HasHostNetworkCommands<RT>,
    HasNetworkProviderManager<RT>, 
    HasLogger<RT>
{
    public static Aff<RT, Unit> checkHostInterfacesWithProgress() =>
        from _ in AnsiConsole<RT>.withProgress(
            "Checking status of host interfaces...",
                HostStateProvider<RT>.checkHostInterfaces)
        select unit;

    public static Aff<RT, HostState> getHostStateWithProgress() =>
        from hostState in AnsiConsole<RT>.withProgress(
            "Analyzing host network settings...", 
            HostStateProvider<RT>.getHostState)
        select hostState;

    public static Aff<RT, (bool IsValid, HostState HostState)> syncCurrentConfigBeforeNewConfig(
        HostState hostState,
        NetworkChanges<RT> currentConfigChanges,
        bool nonInteractive,
        Func<Aff<RT, HostState>> getHostState) =>
        currentConfigChanges.Operations.Match(
            Empty: () => SuccessAff((true, hostState)),
            Seq: _ => syncExistingCurrentConfigChanges(hostState, currentConfigChanges, nonInteractive, getHostState));

    private static Aff<RT, (bool isValid, HostState HostState)> syncExistingCurrentConfigChanges(
        HostState hostState,
        NetworkChanges<RT> currentConfigChanges,
        bool nonInteractive,
        Func<Aff<RT, HostState>> getHostState) =>
        from _1 in AnsiConsole<RT>.write(new Padder(
            new Rows([
                new Text("The currently active configuration is not fully applied on host."),
                new Text("Following changes have to be applied:", new Style(Color.Yellow)),
                ..currentConfigChanges.Operations.Map(op => new Padder(new Text(op.Text), new Padding(2, 0, 0, 0)))
            ]),
            new Padding(0, 1)))
        from syncChanges in nonInteractive
            ? from _2 in AnsiConsole<RT>.writeLine("Non interactive mode - changes will be applied.")
              select true
            : from _3 in AnsiConsole<RT>.write(new Rows(
                  new Text("You can ignore these changes and proceed with validating the new config."),
                  new Text("However, it is recommended to create a valid current state first. "
                           + "With a valid current state a rollback is more likely to succeed in case the new config cannot be applied.")))
              from promptResult in AnsiConsole<RT>.prompt(
                "Apply (a), Ignore (i) or Cancel (c):",
                v => from _ in guard(v is "a" or "i" or "c", Error.New("Please select a valid option."))
                        .ToValidation()
                    select v)
            from _4 in guardnot(promptResult == "c", Errors.Cancelled)
            select promptResult == "a"
        from isValid in syncChanges
            ? executeChangesWithRollback(currentConfigChanges, None, getHostState)
                .Map(_ => true)
            : SuccessAff(false)
        from refreshedHostState in isValid ? getHostState() : SuccessAff(hostState)
        select (isValid, refreshedHostState);

    public static Aff<RT, Unit> syncNetworks() =>
        from _ in AnsiConsole<RT>.withSpinner(
            "Syncing project networks. This could take a while...",
            from syncClient in default(RT).AgentSync
            from _ in syncClient.SendSyncCommand("REBUILD_NETWORKS", CancellationToken.None)
            select unit)
        select unit;

    public static Aff<RT, Unit> validateNetworkImpact(
        NetworkProvidersConfiguration newConfig,
        NetworkProvidersConfiguration currentConfig,
        NetworkProviderDefaults defaults) =>
        from messages in timeout(
            TimeSpan.FromSeconds(10),
            from messages in AnsiConsole<RT>.withSpinner(
                "Validating network impact...",
                from agentSync in default(RT).AgentSync
                from ct in cancelToken<RT>()
                from messages in agentSync.ValidateChanges(newConfig.NetworkProviders, ct)
                select messages)
            select messages)
        from _ in messages.ToSeq().Match(
            Empty: () => unitEff,
            Seq: ms =>
                from _1 in AnsiConsole<RT>.write(new Rows([
                    new Text("Active network settings are incompatible with new configuration:"),
                    ..ms.Map(m => new Padder(new Text(m), new Padding(2, 0, 0, 0)))
                    ]))
                from _5 in FailEff<Unit>(Error.New(
                    "Incompatible network settings detected. You have to remove these settings before applying the new configuration."))
                select unit)
        from _2 in validateMacAddressSpoofingImpact(newConfig, currentConfig, defaults)
        from _3 in validateDhcpGuardImpact(newConfig, currentConfig, defaults)
        from _4 in validateRouterGuardImpact(newConfig, currentConfig, defaults)
        select unit;

    private static Eff<RT, Unit> validateMacAddressSpoofingImpact(
        NetworkProvidersConfiguration newConfig,
        NetworkProvidersConfiguration currentConfig,
        NetworkProviderDefaults defaults) =>
        from _1 in unitEff
        let providersWithDisabledSpoofing = newConfig.NetworkProviders.ToSeq()
            .Filter(np => !np.MacAddressSpoofing.GetValueOrDefault(defaults.MacAddressSpoofing)
                          && currentConfig.NetworkProviders.Any(cp =>
                              cp.Name == np.Name &&
                              cp.MacAddressSpoofing.GetValueOrDefault(defaults.MacAddressSpoofing)))
            .Map(np => np.Name)
        from _2 in providersWithDisabledSpoofing
            .Match(
                Empty: () => unitEff,
                Seq: names => AnsiConsole<RT>.write(new Padder(
                    new Rows([
                        new Text("MAC address spoofing will be disabled for the following providers:"),
                        ..names.Map(n => new Padder(new Text(n), new Padding(2, 0, 0, 0))),
                        new Text("MAC address spoofing will not be automatically disabled for existing catlets."),
                        new Text("Please update any affected catlets manually.")
                    ]),
                    new Padding(0, 1))))
        select unit;

    private static Eff<RT, Unit> validateDhcpGuardImpact(
        NetworkProvidersConfiguration newConfig,
        NetworkProvidersConfiguration currentConfig,
        NetworkProviderDefaults defaults) =>
        from _1 in unitEff
        let providersWithRemovedDisableDhcpGuard = newConfig.NetworkProviders.ToSeq()
            .Filter(np => !np.DisableDhcpGuard.GetValueOrDefault(defaults.DisableDhcpGuard)
                          && currentConfig.NetworkProviders.Any(cp =>
                              cp.Name == np.Name &&
                              cp.DisableDhcpGuard.GetValueOrDefault(defaults.DisableDhcpGuard)))
            .Map(np => np.Name)
        from _2 in providersWithRemovedDisableDhcpGuard
            .Match(
                Empty: () => unitEff,
                Seq: names => AnsiConsole<RT>.write(new Padder(
                    new Rows([
                        new Text("The DHCP guard can no longer be disabled for the following providers:"),
                        ..names.Map(n => new Padder(new Text(n), new Padding(2, 0, 0, 0))),
                        new Text("The DHCP guard will not be automatically re-enabled for existing catlets."),
                        new Text("Please update any affected catlets manually.")
                    ]),
                    new Padding(0, 1))))
        select unit;
    
    private static Eff<RT, Unit> validateRouterGuardImpact(
        NetworkProvidersConfiguration newConfig,
        NetworkProvidersConfiguration currentConfig,
        NetworkProviderDefaults defaults) =>
        from _1 in unitEff
        let providersWithRemovedDisableRouterGuard = newConfig.NetworkProviders.ToSeq()
            .Filter(np => !np.DisableRouterGuard.GetValueOrDefault(defaults.DisableRouterGuard)
                          && currentConfig.NetworkProviders.Any(cp =>
                              cp.Name == np.Name &&
                              cp.DisableRouterGuard.GetValueOrDefault(defaults.DisableRouterGuard)))
            .Map(np => np.Name)
        from _2 in providersWithRemovedDisableRouterGuard
            .Match(
                Empty: () => unitEff,
                Seq: names => AnsiConsole<RT>.write(new Padder(
                    new Rows([
                        new Text("The router guard can no longer be disabled for the following providers:"),
                        ..names.Map(n => new Padder(new Text(n), new Padding(2, 0, 0, 0))),
                        new Text("The router guard will not be automatically re-enabled for existing catlets."),
                        new Text("Please update any affected catlets manually.")
                    ]),
                    new Padding(0, 1))))
        select unit;

    public static Aff<RT, Unit> applyChangesInConsole(
        NetworkChanges<RT> newConfigChanges,
        Func<Aff<RT, HostState>> getHostState,
        bool nonInteractive,
        Option<NetworkProvidersConfiguration> rollbackConfig) =>
        newConfigChanges.Operations.Match(
            Empty: () =>
                AnsiConsole<RT>.writeLine(
                    "The network configuration does not require any changes to the host network."),
            Seq: _ => applyExistingChangesInConsole(newConfigChanges, getHostState, nonInteractive, rollbackConfig));

    private static Aff<RT, Unit> applyExistingChangesInConsole(
        NetworkChanges<RT> newConfigChanges,
        Func<Aff<RT, HostState>> getHostState,
        bool nonInteractive,
        Option<NetworkProvidersConfiguration> rollbackConfig) =>
        from _1 in AnsiConsole<RT>.write(new Padder(
            new Rows([
                new Text("The following changes have to be applied to the host network configuration:"),
                ..newConfigChanges.Operations.Map(op => new Padder(new Text(op.Text), new Padding(2, 0, 0, 0)))
            ]),
            new Padding(0, 1)))
        from _2 in nonInteractive
            ? from _3 in AnsiConsole<RT>.writeLine("Non interactive mode - changes will be applied.")
              select unit
            : from _4 in AnsiConsole<RT>.write(new Rows(
                new Text("Network connectivity may be interrupted while applying these changes.", new Style(Color.Yellow)),
                new Text("In the event of an error, a rollback is attempted.")))
              from promptResult in AnsiConsole<RT>.prompt(
                "Apply (a) or Cancel (c):",
                v => from _ in guard(v is "a" or "c", Error.New("Please select a valid option."))
                        .ToValidation()
                    select v)
              from _5 in guardnot(promptResult == "c", Errors.Cancelled)
              select unit
        from _6 in executeChangesWithRollback(newConfigChanges, rollbackConfig, getHostState)
        select unit;

    private static Aff<RT, Unit> executeChangesWithRollback(
        NetworkChanges<RT> changes,
        Option<NetworkProvidersConfiguration> rollbackConfig,
        Func<Aff<RT, HostState>> getHostState) =>
        from _1 in AnsiConsole<RT>.writeLine("Applying host changes:")
        from _2 in use(
            SuccessAff<RT, ProviderNetworkUpdateState<RT>>(new ProviderNetworkUpdateState<RT>()),
            updateState => executeChanges(changes, updateState)
                           | @catch(e => rollback(e, updateState, rollbackConfig, getHostState)))
        from _3 in AnsiConsole<RT>.writeLine("Host network configuration was updated.")
        select unit;

    private static Aff<RT, Unit> executeChanges(NetworkChanges<RT> changes) =>
        from _1 in use(
            SuccessAff<RT, ProviderNetworkUpdateState<RT>>(new ProviderNetworkUpdateState<RT>()),
            updateState => executeChanges(changes, updateState))
        select unit;

    private static Aff<RT, Unit> executeChanges(
        NetworkChanges<RT> changes,
        ProviderNetworkUpdateState<RT> updateState) =>
        from _ in changes.Operations
            .Map(op => executeChangeOperation(op, updateState))
            .SequenceSerial()
        select unit;

    private static Aff<RT, Unit> executeChangeOperation(
        NetworkChangeOperation<RT> operation,
        ProviderNetworkUpdateState<RT> updateState) =>
        from _1 in AnsiConsole<RT>.write(new Padder(new Text($"{operation.Text}..."), new Padding(2, 0 ,0 ,0)))
        from _2 in operation.Change()
        from _3 in updateState.AddExecutedOperation(operation)
        select unit;

    private static Aff<RT, Unit> rollback(
        Error error,
        ProviderNetworkUpdateState<RT> updateState,
        Option<NetworkProvidersConfiguration> rollbackConfiguration,
        Func<Aff<RT, HostState>> getHostState) =>
        from executedOperations in updateState.GetExecutedOperations()
        from _1 in rollback(executedOperations, rollbackConfiguration, getHostState)
        from _2 in FailAff<RT, Unit>(error)
        select unit;

    private static Aff<RT, Unit> rollback(
        Seq<NetworkChangeOperation<RT>> executedOperations,
        Option<NetworkProvidersConfiguration> rollbackConfiguration,
        Func<Aff<RT, HostState>> getHostState) =>
        from _1 in AnsiConsole<RT>.writeLine("Rolling back changes...")
        let executedOpCodes = executedOperations.Select(o => o.Operation)
        let revertibleOperations = executedOperations
            .Filter(op => (op.CanRollBack?.Invoke(executedOpCodes)).GetValueOrDefault())
            .Filter(op => op.Rollback is not null)
        from _2 in revertibleOperations.Match(
                       Empty: () => unitAff,
                       Seq: rollbackOperations)
                   | @catch(e => AnsiConsole<RT>.write(Renderable(e)))
        from _3 in rollbackConfiguration.Match(
                       Some: c => rollbackToConfig(c, getHostState),
                       None: () => unitAff)
                   | @catch(e => AnsiConsole<RT>.write(Renderable(e)))
        from _4 in !executedOperations.IsEmpty && rollbackConfiguration.IsNone
            ? AnsiConsole<RT>.write(new Rows(
                new Text("Some changes have been rolled back."),
                new Text("Please note that rollback cannot undo all changes, you should check"
                         + " if networking is still working properly in host and catlets.")))
            : unitEff
        select unit;

    private static Aff<RT, Unit> rollbackToConfig(
        NetworkProvidersConfiguration config,
        Func<Aff<RT, HostState>> getHostState) =>
        from _1 in AnsiConsole<RT>.writeLine("Rolling back to previous configuration...")
        from hostState in getHostState()
        from pendingChanges in ProviderNetworkUpdate<RT>.generateChanges(hostState, config, true)
        from _2 in pendingChanges.Operations.Match(
                Empty: () => AnsiConsole<RT>.writeLine("Previous configuration seems to be fully applied."),
                Seq: _ => executeRollbackChanges(pendingChanges))
            .MapFail(e => Error.New("Failed to rollback operations.", e))
        select unit;

    private static Aff<RT, Unit> executeRollbackChanges(
        NetworkChanges<RT> changes) =>
        from _1 in AnsiConsole<RT>.writeLine("Applying necessary changes for rollback...")
        from _2 in executeChanges(changes)
        from _3 in AnsiConsole<RT>.writeLine("Rolled back to previous config.")
        select unit;

    private static Aff<RT, Unit> rollbackOperations(
        Seq<NetworkChangeOperation<RT>> operations) =>
        from _1 in AnsiConsole<RT>.writeLine("Rolling back revertible operations...")
        from _2 in operations
            .Map(rollbackOperation)
            .SequenceSerial()
            .MapFail(e => Error.New("Failed to rollback operations.", e))
        from _3 in AnsiConsole<RT>.writeLine("Revertible operation have been rolled back.")
        select unit;

    private static Aff<RT, Unit> rollbackOperation(
        NetworkChangeOperation<RT> operation) =>
        from _1 in AnsiConsole<RT>.write(new Padder(
            new Text($"Rollback of {operation.Text}..."),
            new Padding(2, 0, 0, 0)))
        from _2 in operation.Rollback!()
        select unit;
}
