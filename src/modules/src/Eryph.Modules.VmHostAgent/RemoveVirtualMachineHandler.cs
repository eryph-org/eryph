using System;
using System.IO;
using System.Threading.Tasks;
using Eryph.Messages.Resources.Machines.Commands;
using Eryph.VmManagement;
using Eryph.VmManagement.Data.Full;
using Eryph.VmManagement.Storage;
using JetBrains.Annotations;
using LanguageExt;
using Rebus.Bus;
using PsVMResult = LanguageExt.Either<Eryph.VmManagement.PowershellFailure, LanguageExt.Unit>;

namespace Eryph.Modules.VmHostAgent
{
    [UsedImplicitly]
    internal class RemoveVirtualMachineHandler : MachineOperationHandlerBase<RemoveVMCommand>
    {
        public RemoveVirtualMachineHandler(IBus bus, IPowershellEngine engine) : base(bus, engine)
        {
        }

        protected override Task<PsVMResult> HandleCommand(TypedPsObject<VirtualMachineInfo> vmInfo,
            RemoveVMCommand command, IPowershellEngine engine)
        {
            var hostSettings = HostSettingsBuilder.GetHostSettings();


            return (from storageSettings in VMStorageSettings.FromVM(hostSettings, vmInfo).ToAsync()
                from stoppedVM in vmInfo.StopIfRunning(engine).ToAsync()
                from _ in stoppedVM.Remove(engine).ToAsync()
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