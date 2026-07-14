using System.Linq;
using System.Threading.Tasks;
using Dbosoft.Rebus.Operations;
using Eryph.GuestServices.HvDataExchange.Host;
using Eryph.Messages.Resources.Catlets.Commands;
using Eryph.Modules.HostAgent.Inventory;
using JetBrains.Annotations;
using LanguageExt;
using LanguageExt.Common;
using Rebus.Handlers;
using static LanguageExt.Prelude;

namespace Eryph.Modules.HostAgent;

/// <summary>
/// Reads the guest's cloud-init provisioning telemetry from the guest KVP pool,
/// decodes it into a rendered log and the reassembled raw events, and returns it
/// to the saga as the <see cref="GetProvisioningLogVMCommandResponse"/>.
/// </summary>
[UsedImplicitly]
internal class GetProvisioningLogVMCommandHandler(
    ITaskMessaging messaging,
    IHostDataExchange hostDataExchange)
    : IHandleMessages<OperationTask<GetProvisioningLogVMCommand>>
{
    public Task Handle(OperationTask<GetProvisioningLogVMCommand> message) =>
        HandleCommand(message.Command).FailOrComplete(messaging, message);

    private EitherAsync<Error, GetProvisioningLogVMCommandResponse> HandleCommand(
        GetProvisioningLogVMCommand command) =>
        from guestData in TryAsync(() => hostDataExchange.GetGuestDataAsync(command.VmId))
            .ToEither(ex => Error.New("Failed to read the provisioning log.", Error.New(ex)))
        let log = CloudInitProvisioningLog.Decode(guestData)
        select new GetProvisioningLogVMCommandResponse
        {
            RenderedLog = log.RenderedText,
            Events = log.Events.ToList(),
        };
}
