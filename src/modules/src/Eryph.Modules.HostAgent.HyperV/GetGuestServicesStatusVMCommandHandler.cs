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
/// Reads the guest's services and provisioning status from the guest KVP pool
/// and returns it to the saga as the <see cref="GetGuestServicesStatusVMCommandResponse"/>.
/// </summary>
[UsedImplicitly]
internal class GetGuestServicesStatusVMCommandHandler(
    ITaskMessaging messaging,
    IGuestStatusReader statusReader)
    : IHandleMessages<OperationTask<GetGuestServicesStatusVMCommand>>
{
    public Task Handle(OperationTask<GetGuestServicesStatusVMCommand> message) =>
        HandleCommand(message.Command).FailOrComplete(messaging, message);

    private EitherAsync<Error, GetGuestServicesStatusVMCommandResponse> HandleCommand(
        GetGuestServicesStatusVMCommand command) =>
        from status in TryAsync(() => statusReader.ReadAsync(command.VmId))
            .ToEither(ex => Error.New("Failed to read the guest services status.", Error.New(ex)))
        select new GetGuestServicesStatusVMCommandResponse
        {
            GuestServicesStatus = status.GuestServicesStatus,
            GuestServicesVersion = status.GuestServicesVersion,
            ProvisioningState = status.ProvisioningState,
            Shell = status.Shell,
            ShellArgs = status.ShellArgs,
        };
}
