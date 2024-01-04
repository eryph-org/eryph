using Dbosoft.Rebus.Operations;
using Eryph.ConfigModel.Catlets;
using Eryph.Core;
using Eryph.Core.VmAgent;
using Eryph.Messages.Resources.Catlets.Commands;
using Eryph.Resources.Machines;
using Eryph.VmManagement;
using Eryph.VmManagement.Data.Full;
using Eryph.VmManagement.Storage;
using Eryph.VmManagement.Tracing;
using JetBrains.Annotations;
using LanguageExt;
using LanguageExt.Common;
using Microsoft.Extensions.Logging;
using Rebus.Bus;

// ReSharper disable ArgumentsStyleAnonymousFunction

namespace Eryph.Modules.VmHostAgent
{
    [UsedImplicitly]
    internal class UpdateCatletVMCommandHandler 
        : CatletConfigCommandHandler<UpdateCatletVMCommand, ConvergeCatletResult>
    {
        private readonly IHostInfoProvider _hostInfoProvider;
        private readonly IVmHostAgentConfigurationManager _vmHostAgentConfigurationManager;

        public UpdateCatletVMCommandHandler(
            IPowershellEngine engine,
            ITaskMessaging messaging,
            ILogger log,
            IHostInfoProvider hostInfoProvider,
            IVmHostAgentConfigurationManager vmHostAgentConfigurationManager) : base(engine, messaging, log)
        {
            _hostInfoProvider = hostInfoProvider;
            _vmHostAgentConfigurationManager = vmHostAgentConfigurationManager;
        }

        protected override EitherAsync<Error, ConvergeCatletResult> HandleCommand(UpdateCatletVMCommand command)
        {
            var config = command.Config;

            var vmId = command.VMId;

            var hostSettings = HostSettingsBuilder.GetHostSettings();
            var convergeVM = Prelude.fun(
                (VmHostAgentConfiguration vmHostAgentConfig, TypedPsObject<VirtualMachineInfo> vmInfo, CatletConfig c, VMStorageSettings storageSettings, VMHostMachineData hostInfo) =>
                    VirtualMachine.Converge(vmHostAgentConfig, hostSettings, hostInfo, Engine, ProgressMessage, vmInfo, c,
                        command.MachineMetadata, command.MachineNetworkSettings, storageSettings));

            return
                from vmHostAgentConfig in _vmHostAgentConfigurationManager.GetCurrentConfiguration()
                from hostInfo in _hostInfoProvider.GetHostInfoAsync(true).WriteTrace()
                from vmList in GetVmInfo(vmId, Engine)
                from vmInfo in EnsureSingleEntry(vmList, vmId)
                from currentStorageSettings in VMStorageSettings.FromVM(vmHostAgentConfig, hostSettings, vmInfo).WriteTrace()
                from plannedStorageSettings in VMStorageSettings.Plan(vmHostAgentConfig, hostSettings, LongToString(command.NewStorageId),
                    config, currentStorageSettings).WriteTrace()
                from metadata in EnsureMetadata(command.MachineMetadata, vmInfo).WriteTrace().ToAsync()
                let mergedConfig = config.GeneticInheritance(metadata.ParentConfig)
                from vmInfoConsistent in EnsureNameConsistent(vmInfo, config, Engine).WriteTrace()
                from vmInfoConverged in convergeVM(vmHostAgentConfig, vmInfoConsistent, mergedConfig, plannedStorageSettings, hostInfo).WriteTrace().ToAsync()
                from inventory in CreateMachineInventory(Engine, vmHostAgentConfig, hostSettings, vmInfoConverged, _hostInfoProvider).WriteTrace()
                select new ConvergeCatletResult
                {
                    Inventory = inventory,
                    MachineMetadata = metadata
                };
        }

    }
};