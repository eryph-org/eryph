using System.Linq;
using System.Threading.Tasks;
using Dbosoft.Rebus.Operations;
using Eryph.Messages.Resources.Catlets.Commands;
using Eryph.Messages.Resources.Catlets.Events;
using Eryph.ModuleCore;
using Eryph.Modules.Controller.DataServices;
using Eryph.Rebus;
using Eryph.Resources;
using JetBrains.Annotations;
using Rebus.Handlers;
using Rebus.Pipeline;

namespace Eryph.Modules.Controller.Inventory
{
    [UsedImplicitly]
    internal class VirtualCatletUpTimeChangedEventHandler : IHandleMessages<VCatletUpTimeChangedEvent>
    {
        private readonly IOperationDispatcher _opDispatcher;
        private readonly IVirtualMachineDataService _vmDataService;
        private readonly IVirtualMachineMetadataService _metadataService;
        private readonly IMessageContext _messageContext;

        public VirtualCatletUpTimeChangedEventHandler(
            IOperationDispatcher opDispatcher, 
            IVirtualMachineMetadataService metadataService, 
            IVirtualMachineDataService vmDataService, IMessageContext messageContext)
        {
            _opDispatcher = opDispatcher;
            _metadataService = metadataService;
            _vmDataService = vmDataService;
            _messageContext = messageContext;
        }

        public Task Handle(VCatletUpTimeChangedEvent message)
        {
            return _vmDataService.GetByVMId(message.VmId).IfSomeAsync(vm =>
            {
                vm.UpTime = message.UpTime;

                return _metadataService.GetMetadata(vm.MetadataId).IfSomeAsync(async metaData =>
                {
                    if (metaData.SensitiveDataHidden)
                        return;

                    var anySensitive = metaData.RaisingConfig?.Config.Any(x => x.Sensitive);

                    if (!anySensitive.GetValueOrDefault())
                        return;

                    if (vm.UpTime.GetValueOrDefault().TotalMinutes >= 5)
                    {
                        metaData.SensitiveDataHidden = true;
                        await _metadataService.SaveMetadata(metaData);

                        await _opDispatcher.StartNew(
                            vm.Project.Id,
                            _messageContext.GetTraceId(),
                            new UpdateConfigDriveCommand
                            {
                                CatletId = vm.Id
                            });
                            
                    }

                });

            });



        }

    }
}