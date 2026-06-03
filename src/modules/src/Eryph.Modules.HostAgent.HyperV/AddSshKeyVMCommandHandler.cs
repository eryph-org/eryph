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
/// BYOK key-install handler for the EGS remote channel: writes the operator's authorized key to the
/// guest KVP slot (with the optional expiry) so the guest authorizes it. Returns an empty completion.
/// </summary>
[UsedImplicitly]
internal class AddSshKeyVMCommandHandler(
    ITaskMessaging messaging,
    IChannelService channelService)
    : IHandleMessages<OperationTask<AddSshKeyVMCommand>>
{
    public Task Handle(OperationTask<AddSshKeyVMCommand> message) =>
        HandleCommand(message.Command).FailOrComplete(messaging, message);

    private EitherAsync<Error, Unit> HandleCommand(AddSshKeyVMCommand command) =>
        TryAsync(async () =>
            {
                await channelService.AddKey(
                    command.VmId, command.SubjectId, command.PublicKey, command.KeyExpiry);
                return unit;
            })
            .ToEither(ex => Error.New("Failed to add the SSH key.", Error.New(ex)));
}
