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
/// Control-plane handler for the EGS remote channel. Writes the operator's authorized key to the guest
/// KVP (when supplied), mints a one-time channel token, and returns the agent's channel endpoint to the
/// saga as the <see cref="OpenSshChannelVMCommandResponse"/>.
/// </summary>
[UsedImplicitly]
internal class OpenSshChannelVMCommandHandler(
    ITaskMessaging messaging,
    IChannelService channelService)
    : IHandleMessages<OperationTask<OpenSshChannelVMCommand>>
{
    public Task Handle(OperationTask<OpenSshChannelVMCommand> message) =>
        HandleCommand(message.Command).FailOrComplete(messaging, message);

    private EitherAsync<Error, OpenSshChannelVMCommandResponse> HandleCommand(
        OpenSshChannelVMCommand command) =>
        from registration in TryAsync(() => channelService.RegisterChannel(
                command.VmId,
                command.SubjectId,
                command.PublicKey,
                command.KeyExpiry))
            .ToEither(ex => Error.New("Failed to open the SSH channel.", Error.New(ex)))
        select new OpenSshChannelVMCommandResponse
        {
            Token = registration.Token,
            AgentEndpoint = registration.AgentEndpoint,
            ExpiresAt = registration.ExpiresAt,
        };
}
