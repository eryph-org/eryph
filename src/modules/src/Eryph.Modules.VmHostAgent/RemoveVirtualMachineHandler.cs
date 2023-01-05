using System;
using System.IO;
using System.Threading.Tasks;
using Eryph.Messages.Resources.Catlets.Commands;
using Eryph.VmManagement;
using Eryph.VmManagement.Data.Full;
using Eryph.VmManagement.Storage;
using JetBrains.Annotations;
using LanguageExt;
using LanguageExt.Common;
using Rebus.Bus;

namespace Eryph.Modules.VmHostAgent
{
    [UsedImplicitly]
    internal class RemoveVirtualMachineHandler : VCatletOperationHandlerBase<RemoveVirtualCatletCommand>
    {
        public RemoveVirtualMachineHandler(IBus bus, IPowershellEngine engine) : base(bus, engine)
        {
        }

        protected override Task<Either<Error, Unit>> HandleCommand(TypedPsObject<VirtualMachineInfo> vmInfo,
            RemoveVirtualCatletCommand command, IPowershellEngine engine)
        {
            var hostSettings = HostSettingsBuilder.GetHostSettings();


            return (from storageSettings in VMStorageSettings.FromVM(hostSettings, vmInfo)
                from stoppedVM in vmInfo.StopIfRunning(engine).ToAsync().ToError()
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