using Eryph.AnsiConsole.Sys;
using Eryph.Core.Network;
using Eryph.Modules.HostAgent.Networks.OVS;
using Eryph.VmManagement.Sys;
using Humanizer;
using LanguageExt;
using LanguageExt.Common;
using LanguageExt.Effects.Traits;
using LanguageExt.Sys;
using LanguageExt.Sys.Traits;
using System;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Channels;
using Spectre.Console;

using static LanguageExt.Prelude;
using static Eryph.AnsiConsole.Prelude;

namespace Eryph.Modules.HostAgent.Networks;

public static class ProviderNetworkUpdateInConsole<RT>
    where RT : struct,
    HasAnsiConsole<RT>,
    HasConsole<RT>,
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

    private static Aff<RT, Unit> rollback(
        Error error,
        Seq<NetworkChangeOperation<RT>> executedOperations,
        Option<NetworkProvidersConfiguration> rollbackConfiguration) =>
        from _1 in rollback(executedOperations, rollbackConfiguration)
        from _2 in FailAff<RT, Unit>(error)
        select unit;

    private static Aff<RT, Unit> rollback(
        Seq<NetworkChangeOperation<RT>> executedOperations,
        Option<NetworkProvidersConfiguration> rollbackConfiguration) =>
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
                       Some: rollbackToConfig,
                       None: () => unitAff)
                   | @catch(e => AnsiConsole<RT>.write(Renderable(e)))
        from _4 in !executedOperations.IsEmpty && rollbackConfiguration.IsNone
            ? AnsiConsole<RT>.write(new Rows(
                new Text("Some changes have been rolled back."),
                new Text("Please note that rollback cannot undo all changes, you should check"
                         + " if networking is still working properly in host and catlets.")))
            : unitEff
        select unit;

    private static Aff<RT, Unit> rollbackToCurrentConfig(
        Error error,
        NetworkProvidersConfiguration currentConfig,
        Func<Aff<RT, HostState>> getHostState)
    {
        return (
                from m1 in Console<RT>.writeLine(
                    $"\nError: {error}" +
                    "\nFailed to apply new configuration. Rolling back to current active configuration.\n")
                from hostState in getHostState()
                from currentConfigChanges in ProviderNetworkUpdate<RT>
                    .generateChanges(hostState, currentConfig, true)
                from _ in currentConfigChanges.Operations.Length == 0
                    ? Console<RT>.writeLine(
                        "No changes found to be rolled back." +
                        "\nPlease note that rollback cannot undo all changes, you should check" +
                        "\nif networking is still working properly in host and catlets.")
                    : from _ in executeChangesConsole(currentConfigChanges)
                    from m2 in Console<RT>.writeLine("Rollback complete")
                    select unit
                select unit
            )
            // if rollback fails output error inf rollback
            .IfFailAff(f =>
                Console<RT>.writeLine($"\nError: {f}" + "\nFailed to rollback.\n")
            )

            //always exit rollback with a error
            .Bind(_ => FailAff<Unit>(error));
    }

    private static Aff<RT, Unit> rollbackToConfig(
        NetworkProvidersConfiguration config) =>
        from _1 in AnsiConsole<RT>.writeLine("Rolling back to previous configuration...")
        from hostState in getHostStateWithProgress()
        from pendingChanges in ProviderNetworkUpdate<RT>.generateChanges(hostState, config, true)
        from _2 in pendingChanges.Operations.Match(
                Empty: () => AnsiConsole<RT>.writeLine("Previous configuration seems to be fully applied."),
                Seq: _ => executeRollbackChanges(pendingChanges))
            .MapFail(e => Error.New("Failed to rollback operations.", e))
        select unit;

    private static Aff<RT, Unit> executeRollbackChanges(
        NetworkChanges<RT> changes) =>
        from _1 in AnsiConsole<RT>.writeLine("Applying necessary changes for rollback...")
        from _2 in executeChangesConsole(changes)
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
        from _1 in AnsiConsole<RT>.writeLine($"rollback of: {operation.Text}")
        from _2 in operation.Rollback!()
        select unit;

    private static Aff<RT, T> minRollbackChanges<T>(Error error, bool fullRollbackFollows)
    {
        if (error is not OperationError opError)
            return FailAff<T>(error);

        var rollbackMessage = false;
        var executedOpCodes = opError.ExecutedOperations
            .Select(o => o.Operation);
        return default(RT).ConsoleEff.Bind(console =>
                opError.ExecutedOperations.Where(o => (
                        o.CanRollBack?.Invoke(executedOpCodes)).GetValueOrDefault())
                    .Where(o => o.Rollback != null)
                    .Map(o =>
                    {
                        if (!rollbackMessage)
                            console.WriteLine("=> Operation failed. Trying to rollback.");
                        rollbackMessage = true;
                        console.WriteLine($"rollback of: {o.Text}");
                        return o.Rollback!();
                    }).TraverseSerial(l => l)
                    .Bind(_ =>
                    {
                        if (rollbackMessage && !fullRollbackFollows)
                        {
                            console.WriteLine("\nA rollback was executed for at least a part of the changes." +
                                              "\nPlease note that rollback cannot undo all changes, you should check" +
                                              "\nif networking is still working properly in host and catlets.");
                        }

                        return FailAff<T>(opError.Cause);
                    }));
    }

    private record OperationError([property: DataMember] Seq<NetworkChangeOperation<RT>> ExecutedOperations, Error Cause)
        : Expected("Operation failed", 100, Cause);


    private static Aff<RT, Unit> executeChangesConsole(NetworkChanges<RT> changes) =>
        changes.Operations.Match(
            Empty: () => unitAff,
            Seq: _ => executeChangeOperations(changes));

    private static Aff<RT, Unit> executeChangeOperations(
        NetworkChanges<RT> changes) =>
        from _1 in AnsiConsole<RT>.writeLine("Applying host changes:")
        from _2 in use(
            SuccessAff<RT, ProviderNetworkUpdateState<RT>>(new ProviderNetworkUpdateState<RT>()),
            updateState => executeChangeOperations(changes, updateState)
                           |@catch(e => from executedOperations in updateState.GetExecutedOperations()
                                        from _ in FailAff<RT, Unit>(new OperationError(executedOperations, e))
                                        select unit))
        from _3 in AnsiConsole<RT>.writeLine("Host network configuration was updated.")
        select unit;

    private static Aff<RT, Unit> executeChangeOperations(
        NetworkChanges<RT> changes,
        ProviderNetworkUpdateState<RT> updateState) =>
        from _ in changes.Operations
            .Map(op => executeChangeOperation(op, updateState))
            .SequenceSerial()
        select unit;

    private static Aff<RT, Unit> executeChangeOperation(
        NetworkChangeOperation<RT> operation,
        ProviderNetworkUpdateState<RT> updateState) =>
        from _1 in AnsiConsole<RT>.writeLine($"running: {operation.Text}")
        from _2 in operation.Change()
        from _3 in updateState.AddExecutedOperation(operation)
        select unit;

    public static Aff<RT, (bool IsValid, bool RefreshState)> syncCurrentConfigBeforeNewConfig(
        HostState hostState,
        NetworkChanges<RT> currentConfigChanges,
        bool nonInteractive) =>
        currentConfigChanges.Operations.Match(
            Empty: () => SuccessAff((true, false)),
            Seq: _ => trySyncCurrentConfigChanges(currentConfigChanges, nonInteractive));

    private static Aff<RT, (bool isValid, bool refreshState)> trySyncCurrentConfigChanges(
        NetworkChanges<RT> currentConfigChanges,
        bool nonInteractive) =>
        from _1 in SuccessAff(unit)
        let rows = Seq1(new Text("The currently active configuration is not fully applied on host."))
                   + Seq1(new Text("Following changes have to be applied:", new Style(Color.Yellow)))
                   + currentConfigChanges.Operations.Map(op => new Text(op.Text))
        from _2 in AnsiConsole<RT>.write(new Rows(rows))
        from syncChanges in nonInteractive
            ? from _1 in AnsiConsole<RT>.writeLine("Non interactive mode - changes will be applied.")
              select true
            : from _2 in AnsiConsole<RT>.write(new Rows(
                  new Text("You can ignore these changes and proceed with validating the new config."),
                  new Text("However, it is recommended to create a valid current state first. "
                           + "With a valid current state a rollback is more likely to succeed in case the new config cannot be applied.")))
              from promptResult in AnsiConsole<RT>.prompt(
                "Apply (a), Ignore (i) or Cancel (c):",
                v => from _ in guard(v is "a" or "i" or "c", Error.New("Please select a valid option."))
                        .ToValidation()
                    select v)
            from _3 in guardnot(promptResult == "c", Errors.Cancelled)
            select promptResult == "a"
        from isValid in syncChanges
            ? executeChangesConsole(currentConfigChanges)
                .Catch(e => rollback(e, ))
                .Map(_ => true)
                .IfFailAff(f => minRollbackChanges<bool>(f, false))
            : SuccessAff(false)
        select (isValid, isValid);

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
                    ..ms.Map(m => new Text(m))
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
                Seq: names => AnsiConsole<RT>.write(new Rows([
                    new Text( "MAC address spoofing will be disabled for the following providers:"),
                    ..names.Map(n => new Text(n)),
                    new Text("MAC address spoofing will not be automatically disabled for existing catlets."),
                    new Text("Please update any affected catlets manually.")
                ])))
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
                Seq: names => AnsiConsole<RT>.write(new Rows([
                    new Text("The DHCP guard can no longer be disabled for the following providers:"),
                    ..names.Map(n => new Text(n)),
                    new Text("The DHCP guard will not be automatically re-enabled for existing catlets."),
                    new Text("Please update any affected catlets manually.")
                ])))
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
                Seq: names => AnsiConsole<RT>.write(new Rows([
                    new Text("The router guard can no longer be disabled for the following providers:"),
                    ..names.Map(n => new Text(n)),
                    new Text("The router guard will not be automatically re-enabled for existing catlets."),
                    new Text("Please update any affected catlets manually.")
                ])))
        select unit;

    public static Aff<RT, Unit> applyChangesInConsole(
        NetworkProvidersConfiguration currentConfig,
        NetworkChanges<RT> newConfigChanges,
        Func<Aff<RT, HostState>> getHostState,
        bool nonInteractive,
        bool rollbackToCurrent)
    {
        return default(RT).ConsoleEff.Bind(console =>
        {

            Eff<RT, Unit> InteractiveCheck() =>
                from m1 in Console<RT>.writeLine(
                    "\nThe following changes have to be applied to the host network configuration:")
                from m2 in newConfigChanges.Operations
                    .Map(op => Console<RT>.writeLine("- " + op.Text))
                    .Traverse(l => l)

                from output in !nonInteractive
                    ? from m3 in Console<RT>.writeLine(
                        "\nNetwork connectivity may be interrupted while applying these changes." +
                        "\nIn the event of an error, a rollback is attempted.\n")

                    from decision in repeat(
                                         from _ in Console<RT>.write("\nApply (a) or Cancel (c): ")
                                         from l in Console<RT>.readLine
                                         let input = l.ToLowerInvariant()
                                         from f1 in guardnot(input == "c", Errors.Cancelled)
                                         from f2 in guardnot(input == "a", Error.New(100, "apply"))
                                         from _1 in Console<RT>.writeLine("Invalid input. Accepted input: a, or c")
                                         select l)
                                     | @catch(100, "a")
                    select unit
                    : from m4 in Console<RT>.writeLine("Non interactive mode - changes will be applied.")
                    select unit
                select output;

            var isEmpty = newConfigChanges.Operations.Length == 0;

            return (!isEmpty
                    ? InteractiveCheck()
                    : Console<RT>.writeLine("The network configuration does not require any changes to the host network."))
                .Bind(_ => executeChangesConsole(newConfigChanges)

                    // rollback changes that have a direct rollback attached
                    .IfFailAff(f => minRollbackChanges<Unit>(f, rollbackToCurrent))

                    // try to rollback to a valid, current config
                    .IfFailAff(f =>
                        rollbackToCurrent ?
                            rollbackToCurrentConfig(f, currentConfig, getHostState)
                            : FailAff<Unit>(f)));

        });
    }
}
