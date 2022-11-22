using System.Runtime.Serialization;
using Eryph.Modules.VmHostAgent.Networks.OVS;
using Eryph.Modules.VmHostAgent.Networks.Settings;
using LanguageExt;
using LanguageExt.Common;
using LanguageExt.Effects.Traits;
using LanguageExt.Sys;
using LanguageExt.Sys.Traits;

namespace Eryph.Modules.VmHostAgent.Networks;

public static class ProviderNetworkUpdateInConsole<RT>
    where RT : struct,
    HasConsole<RT>,
    HasCancel<RT>,
    HasOVSControl<RT>,
    HasAgentSyncClient<RT>,
    HasHostNetworkCommands<RT>,
    HasNetworkProviderManager<RT>, 
    HasLogger<RT>

{
    public static Aff<RT, HostState> getHostStateWithProgress() =>
        from _ in Console<RT>.write("Analyzing host network settings => ")
        // collect network state of host
        from hostState in ProviderNetworkUpdate<RT>.getHostState(
            () => Console<RT>.write("."))
        from __ in Console<RT>.writeEmptyLine
        select hostState;

    private static Aff<RT, Unit> rollbackToCurrentConfig(Error error, NetworkProvidersConfiguration currentConfig)
    {
        return (
                from m1 in Console<RT>.writeLine(
                    $"\nError: {error}" +
                    "\nFailed to apply new configuration. Rolling back to current active configuration.\n")
                from hostState in getHostStateWithProgress()
                from currentConfigChanges in ProviderNetworkUpdate<RT>
                    .generateChanges(hostState, currentConfig)
                from _ in currentConfigChanges.Operations.Length == 0
                    ? Console<RT>.writeLine(
                        "No changes found to be rolled back." +
                        "\nPlease note that rollback cannot undo all changes, you should check" +
                        "\nif networking is still working probably in host and catlets.")
                    : from _ in executeChangesConsole(currentConfigChanges)
                    from m2 in Console<RT>.writeLine("Rollback complete")
                    select Prelude.unit
                select Prelude.unit
            )
            // if rollback fails output error inf rollback
            .IfFailAff(f =>
                Console<RT>.writeLine($"\nError: {f}" + "\nFailed to rollback.\n")
            )

            //always exit rollback with a error
            .Bind(_ => Prelude.FailAff<Unit>(error));

    }

    private static Aff<RT, T> minRollbackChanges<T>(Error error, bool fullRollbackFollows)
    {
        if (error is not OperationError opError)
            return Prelude.FailAff<T>(error);

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
                                              "\nif networking is still working probably in host and catlets.");
                        }

                        return Prelude.FailAff<T>(opError.Cause);
                    }))
            ;

    }

    private record OperationError([property: DataMember] Seq<NetworkChangeOperation<RT>> ExecutedOperations, Error Cause)
        : Expected("Operation failed", 100, Cause);


    private static Aff<RT, Unit> executeChangesConsole(NetworkChanges<RT> changes)
    {
        Seq<NetworkChangeOperation<RT>> executedOps = default;

        if (changes.Operations.Length == 0)
            return Prelude.unitAff;

        return
            (from m1 in Console<RT>.writeLine("\nApplying host changes:")
                from ops in changes.Operations.Map(o =>
                    from m2 in Console<RT>.write($"\nrunning: {o.Text}  ")
                    from _ in o.Change().Map(
                        r =>
                        {
                            executedOps = executedOps.Add(o);
                            return r;
                        }
                    )
                    select Prelude.unit).TraverseSerial(l => l)
                select Prelude.unit)

            .Bind(_ => Console<RT>.writeLine("\nHost network configuration was updated.\n"))
            .Map(_ => Prelude.unit)
            .MapFail(e => new OperationError(executedOps, e));
    }

    public static Aff<RT, (bool IsValid, HostState HostState)> syncCurrentConfigBeforeNewConfig(
        HostState hostState,
        NetworkChanges<RT> currentConfigChanges,
        bool nonInteractive)
    {
        Eff<RT, bool> InteractiveCheck() =>
            from m1 in Console<RT>.writeLine("The currently active configuration is not fully applied on host." +
                                             "\nFollowing changes have to be applied:")
            from m2 in currentConfigChanges.Operations
                .Map(op => Console<RT>.writeLine("- " + op.Text))
                .Traverse(l => l)

            from output in !nonInteractive
                ? from m3 in Console<RT>.writeLine(
                    "\nYou can ignore these changes and proceed with validating the new config." +
                    "\nHowever - a valid current state is recommended, as in case a error occurs " +
                    "\nwhile applying the new config a full rollback can be done.\n")

                from decision in Prelude.repeat(
                                     from _ in Console<RT>.write("\nApply (a), Ignore (i) or Cancel (c): ")
                                     from l in Console<RT>.readLine
                                     let input = l.ToLowerInvariant()
                                     from f1 in Prelude.guardnot(input == "c", Errors.Cancelled)
                                     from f2 in Prelude.guardnot(input == "a", Error.New(100, "apply"))
                                     from f3 in Prelude.guardnot(input == "i", Error.New(200, "ignore"))
                                     from _1 in Console<RT>.writeLine("Invalid input. Accepted input: a, i or c")
                                     select l)
                                 | Prelude.@catch(100, "a")
                                 | Prelude.@catch(200, "i")
                select decision == "a"
                : from m4 in Console<RT>.writeLine("Non interactive mode - changes will be applied.")
                select true
            select output;


        var isEmpty = currentConfigChanges.Operations.Length == 0;

        return from run in isEmpty ? Prelude.SuccessEff(true) : InteractiveCheck()
            from isValid in run
                ? // apply changes or empty changes
                executeChangesConsole(currentConfigChanges)
                    .Map(_ => true)
                    .IfFailAff(f => minRollbackChanges<bool>(f, false))
                : // config not valid (ignored) 
                Prelude.SuccessAff(false)

            // refresh host state if new config was applied
            from newHostState in isValid && !isEmpty
                ? getHostStateWithProgress()
                : Prelude.SuccessAff(hostState)
            select (isValid, newHostState);


    }

    public static Aff<RT, Unit> applyChangesInConsole(
        NetworkProvidersConfiguration currentConfig,
        NetworkChanges<RT> newConfigChanges,
        bool nonInteractive,
        bool rollbackToCurrent)
    {
        return default(RT).ConsoleEff.Bind(console =>
        {

            Eff<RT, Unit> InteractiveCheck() =>
                from m1 in Console<RT>.writeLine(
                    "\nFollowing changes have to be applied on host networking configuration:")
                from m2 in newConfigChanges.Operations
                    .Map(op => Console<RT>.writeLine("- " + op.Text))
                    .Traverse(l => l)

                from output in !nonInteractive
                    ? from m3 in Console<RT>.writeLine(
                        "\nNetwork connectivity may be interrupted while applying these changes." +
                        "\nIn case of failure a rollback will be tried.\n")

                    from decision in Prelude.repeat(
                                         from _ in Console<RT>.write("\nApply (a) or Cancel (c): ")
                                         from l in Console<RT>.readLine
                                         let input = l.ToLowerInvariant()
                                         from f1 in Prelude.guardnot(input == "c", Errors.Cancelled)
                                         from f2 in Prelude.guardnot(input == "a", Error.New(100, "apply"))
                                         from _1 in Console<RT>.writeLine("Invalid input. Accepted input: a, or c")
                                         select l)
                                     | Prelude.@catch(100, "a")
                    select Prelude.unit
                    : from m4 in Console<RT>.writeLine("Non interactive mode - changes will be applied.")
                    select Prelude.unit
                select output;

            var isEmpty = newConfigChanges.Operations.Length == 0;

            return (!isEmpty
                    ? InteractiveCheck()
                    : Console<RT>.writeLine("New network configuration requires no changes on host networking."))
                .Bind(_ => executeChangesConsole(newConfigChanges)

                    // rollback changes that have a direct rollback attached
                    .IfFailAff(f => minRollbackChanges<Unit>(f, rollbackToCurrent))

                    // try to rollback to a valid, current config
                    .IfFailAff(f =>
                        rollbackToCurrent ?
                            rollbackToCurrentConfig(f, currentConfig)
                            : Prelude.FailAff<Unit>(f)));

        });
    }

}