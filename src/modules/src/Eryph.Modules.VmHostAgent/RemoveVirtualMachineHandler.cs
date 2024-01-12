using System;
using System.IO;
using System.Threading.Tasks;
using Dbosoft.Rebus.Operations;
using Eryph.Core;
using Eryph.Messages.Resources.Catlets.Commands;
using Eryph.Modules.VmHostAgent.Networks.OVS;
using Eryph.VmManagement;
using Eryph.VmManagement.Data.Full;
using Eryph.VmManagement.Storage;
using JetBrains.Annotations;
using LanguageExt;
using LanguageExt.Common;

namespace Eryph.Modules.VmHostAgent
{
    [UsedImplicitly]
    internal class RemoveVirtualMachineHandler : CatletOperationHandlerBase<RemoveCatletVMCommand>
    {
        IOVSPortManager _ovsPortManager;
        private readonly IVmHostAgentConfigurationManager _vmHostAgentConfigurationManager;

        public RemoveVirtualMachineHandler(
            ITaskMessaging messaging,
            IPowershellEngine engine,
            IOVSPortManager ovsPortManager,
            IVmHostAgentConfigurationManager vmHostAgentConfigurationManager) : base(messaging, engine)
        {
            _ovsPortManager = ovsPortManager;
            _vmHostAgentConfigurationManager = vmHostAgentConfigurationManager;
        }

        protected override Task<Either<Error, Unit>> HandleCommand(TypedPsObject<VirtualMachineInfo> vmInfo,
            RemoveCatletVMCommand command, IPowershellEngine engine)
        {
            var hostSettings = HostSettingsBuilder.GetHostSettings();


            return (from vmHostAgentConfig in _vmHostAgentConfigurationManager.GetCurrentConfiguration(hostSettings)
                from storageSettings in VMStorageSettings.FromVM(vmHostAgentConfig, hostSettings, vmInfo)
                from stoppedVM in vmInfo.StopIfRunning(engine).ToAsync().ToError()
                from uRemovePorts in _ovsPortManager.SyncPorts(vmInfo, VMPortChange.Remove)
                from _ in stoppedVM.Remove(engine).ToAsync().ToError()
                let __ = RemoveVMFiles(storageSettings)
                select Unit.Default)
                .ToEither();

        }

        protected override PsCommandBuilder CreateGetVMCommand(Guid vmId)
        {
            return base.CreateGetVMCommand(vmId).AddParameter("ErrorAction", "SilentlyContinue");
        }

        private static Unit RemoveVMFiles(Option<VMStorageSettings> storageSettings)

        {
            return storageSettings.IfSome(settings =>
            {
                if (settings.Frozen)
                {
                    return;
                }

                settings.StorageIdentifier.IfSome(storageId =>
                {

                    var vmDataPath = Path.Combine(settings.VMPath, storageId);

                    if (Directory.Exists(vmDataPath))
                    {
                        Directory.Delete(vmDataPath, true);
                    }
                });

            });
        }
    }
}