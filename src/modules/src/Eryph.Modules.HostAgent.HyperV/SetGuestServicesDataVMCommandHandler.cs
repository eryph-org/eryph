using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Dbosoft.Rebus.Operations;
using Eryph.Messages.Resources.Catlets.Commands;
using JetBrains.Annotations;
using LanguageExt;
using LanguageExt.Common;
using Rebus.Handlers;
using static LanguageExt.Prelude;

namespace Eryph.Modules.HostAgent;

/// <summary>
/// Generic guest-services write handler: applies the command's External-pool
/// KVP values to the guest. Shared by every setting (shell, authorized keys, ...).
/// </summary>
[UsedImplicitly]
internal class SetGuestServicesDataVMCommandHandler(
    ITaskMessaging messaging,
    IGuestDataWriter writer)
    : IHandleMessages<OperationTask<SetGuestServicesDataVMCommand>>
{
    public Task Handle(OperationTask<SetGuestServicesDataVMCommand> message) =>
        HandleCommand(message.Command).FailOrComplete(messaging, message);

    private EitherAsync<Error, Unit> HandleCommand(SetGuestServicesDataVMCommand command) =>
        TryAsync(async () =>
            {
                var values = new Dictionary<string, string?>(StringComparer.Ordinal);
                foreach (var (key, value) in command.Values)
                    values[key] = value;
                foreach (var key in command.RemoveKeys)
                    values[key] = null;

                await writer.SetExternalAsync(command.VmId, values);
                return unit;
            })
            .ToEither(ex => Error.New("Failed to set the guest services data.", Error.New(ex)));
}
