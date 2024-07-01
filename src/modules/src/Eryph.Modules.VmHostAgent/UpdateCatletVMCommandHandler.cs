using System;
using Dbosoft.Rebus.Operations;
using Eryph.ConfigModel;
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
        private readonly IHostSettingsProvider _hostSettingsProvider;
        private readonly IVmHostAgentConfigurationManager _vmHostAgentConfigurationManager;

        public UpdateCatletVMCommandHandler(
            IPowershellEngine engine,
            ITaskMessaging messaging,
            ILogger log,
            IHostInfoProvider hostInfoProvider,
            IHostSettingsProvider hostSettingsProvider,
            IVmHostAgentConfigurationManager vmHostAgentConfigurationManager) : base(engine, messaging, log)
        {
            _hostInfoProvider = hostInfoProvider;
            _hostSettingsProvider = hostSettingsProvider;
            _vmHostAgentConfigurationManager = vmHostAgentConfigurationManager;
        }

        protected override EitherAsync<Error, ConvergeCatletResult> HandleCommand(UpdateCatletVMCommand command)
        {
            var config = command.Config;

            var vmId = command.VMId;

            var convergeVM = Prelude.fun(
                (VmHostAgentConfiguration vmHostAgentConfig, TypedPsObject<VirtualMachineInfo> vmInfo, CatletConfig c, VMStorageSettings storageSettings, VMHostMachineData hostInfo) =>
                    VirtualMachine.Converge(vmHostAgentConfig, hostInfo, Engine, ProgressMessage, vmInfo, c,
                        command.MachineMetadata, command.MachineNetworkSettings, storageSettings));

            return
                from hostSettings in _hostSettingsProvider.GetHostSettings()
                from vmHostAgentConfig in _vmHostAgentConfigurationManager.GetCurrentConfiguration(hostSettings)
                from hostInfo in _hostInfoProvider.GetHostInfoAsync(true).WriteTrace()
                from vmList in GetVmInfo(vmId, Engine)
                from vmInfo in EnsureSingleEntry(vmList, vmId)
                from currentStorageSettings in VMStorageSettings.FromVM(vmHostAgentConfig, vmInfo).WriteTrace()
                from plannedStorageSettings in VMStorageSettings.Plan(vmHostAgentConfig, LongToString(command.NewStorageId),
                    config, currentStorageSettings).WriteTrace()
                from metadata in EnsureMetadata(command.MachineMetadata, vmInfo).WriteTrace().ToAsync()
                let genepoolReader = new LocalGenepoolReader(vmHostAgentConfig)
                from mergedConfig in config.BreedAndFeed(genepoolReader, metadata.ParentConfig)
                    .Map(c => c.FeedSystemVariables(metadata))
                    .ToAsync()
                from substitutedConfig in CatletConfigVariableSubstitutions.SubstituteVariables(mergedConfig)
                    .ToEither()
                    .MapLeft(issues => Error.New("The substitution of variables failed.", Error.Many(issues.Map(i => i.ToError()))))
                    .ToAsync()
                from vmInfoConsistent in EnsureNameConsistent(vmInfo, config, Engine).WriteTrace()
                from vmInfoConverged in convergeVM(vmHostAgentConfig, vmInfoConsistent, substitutedConfig, plannedStorageSettings, hostInfo).WriteTrace().ToAsync()
                from inventory in CreateMachineInventory(Engine, vmHostAgentConfig, vmInfoConverged, _hostInfoProvider).WriteTrace()
                select new ConvergeCatletResult
                {
                    Inventory = inventory,
                    MachineMetadata = metadata,
                    Timestamp = DateTimeOffset.UtcNow,
                };
        }

    }
}