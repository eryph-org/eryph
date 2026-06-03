using System.Threading.Tasks;
using Dbosoft.Rebus.Operations;
using Eryph.Messages.Resources.Catlets.Commands;
using Eryph.Modules.HostAgent.Channels;
using JetBrains.Annotations;
using LanguageExt;
using LanguageExt.Common;
using Rebus.Handlers;

using static LanguageExt.Prelude;

namespace Eryph.Modules.HostAgent;

/// <summary>
/// Revoke handler for the EGS remote channel: clears the operator's authorized-key KVP slot on the
/// guest so the key no longer authorizes. Returns an empty completion.
/// </summary>
[UsedImplicitly]
internal class RemoveSshKeyVMCommandHandler(
    ITaskMessaging messaging,
    IChannelService channelService)
    : IHandleMessages<OperationTask<RemoveSshKeyVMCommand>>
{
    public Task Handle(OperationTask<RemoveSshKeyVMCommand> message) =>
        HandleCommand(message.Command).FailOrComplete(messaging, message);

    private EitherAsync<Error, Unit> HandleCommand(RemoveSshKeyVMCommand command) =>
        TryAsync(async () =>
            {
                await channelService.RemoveKey(command.VmId, command.SubjectId);
                return unit;
            })
            .ToEither(ex => Error.New("Failed to remove the SSH key.", Error.New(ex)));
}
