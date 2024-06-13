using System.Linq;
using System.Threading.Tasks;
using Dbosoft.Rebus.Operations;
using Eryph.Messages.Resources.Catlets.Commands;
using Eryph.Messages.Resources.Catlets.Events;
using Eryph.ModuleCore;
using Eryph.Modules.Controller.DataServices;
using Eryph.Rebus;
using JetBrains.Annotations;
using Rebus.Handlers;
using Rebus.Pipeline;

namespace Eryph.Modules.Controller.Inventory;

[UsedImplicitly]
internal class CatletUpTimeChangedEventHandler(
    IOperationDispatcher opDispatcher,
    IVirtualMachineMetadataService metadataService,
    IVirtualMachineDataService vmDataService,
    IMessageContext messageContext)
    : IHandleMessages<CatletUpTimeChangedEvent>
{
    public Task Handle(CatletUpTimeChangedEvent message)
    {
        return vmDataService.GetByVMId(message.VmId).IfSomeAsync(vm =>
        {
            vm.UpTime = message.UpTime;

            return metadataService.GetMetadata(vm.MetadataId).IfSomeAsync(async metaData =>
            {
                if (metaData.SecureDataHidden)
                    return;

                var anySensitive = metaData.Fodder.ToSeq().Exists(
                                       f => f.Secret.GetValueOrDefault()
                                            || f.Variables.ToSeq().Exists(v => v.Secret.GetValueOrDefault()))
                                   || metaData.Variables.ToSeq().Exists(v => v.Secret.GetValueOrDefault());

                if (!anySensitive)
                    return;

                if (vm.UpTime.GetValueOrDefault().TotalMinutes >= 2)
                {
                    metaData.SecureDataHidden = true;
                    await metadataService.SaveMetadata(metaData);

                    await opDispatcher.StartNew(
                        vm.Project.Id,
                        messageContext.GetTraceId(),
                        new UpdateConfigDriveCommand
                        {
                            CatletId = vm.Id
                        });
                }
            });
        });
    }
}
