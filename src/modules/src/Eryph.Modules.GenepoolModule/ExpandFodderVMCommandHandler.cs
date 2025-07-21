using System;
using System.Collections.Generic;
using System.IO.Abstractions;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dbosoft.Rebus.Operations;
using Eryph.Core;
using Eryph.Core.VmAgent;
using Eryph.Messages.Resources.Catlets.Commands;
using Eryph.VmManagement;
using JetBrains.Annotations;
using LanguageExt;
using LanguageExt.Common;
using Rebus.Handlers;

namespace Eryph.Modules.Genepool;

[UsedImplicitly]
internal class ExpandFodderVMCommandHandler(
    IFileSystemService fileSystem,
    ITaskMessaging messaging,
    IHostSettingsProvider hostSettingsProvider,
    IVmHostAgentConfigurationManager vmHostAgentConfigurationManager)
    : IHandleMessages<OperationTask<ExpandFodderVMCommand>>
{
    public Task Handle(OperationTask<ExpandFodderVMCommand> message) =>
        HandleCommand(message.Command).FailOrComplete(messaging, message);

    private EitherAsync<Error, ExpandFodderVMCommandResponse> HandleCommand(
        ExpandFodderVMCommand command) =>
        from hostSettings in hostSettingsProvider.GetHostSettings()
        from vmHostAgentConfig in vmHostAgentConfigurationManager.GetCurrentConfiguration(hostSettings)
        let genepoolReader = new LocalGenepoolReader(fileSystem, vmHostAgentConfig)
        let configWithSystemVariables = command.CatletMetadata is not null
            ? CatletFeeding.FeedSystemVariables(command.Config, command.CatletMetadata)
            : CatletFeeding.FeedSystemVariables(command.Config, "#catletId", "#vmId")
        from fedConfig in CatletFeeding.Feed(
            configWithSystemVariables,
            command.ResolvedGenes.ToSeq(),
            genepoolReader)
        from substitutedConfig in CatletConfigVariableSubstitutions.SubstituteVariables(fedConfig)
            .ToEither()
            .MapLeft(issues => Error.New("The substitution of variables failed.", Error.Many(issues.Map(i => i.ToError()))))
            .ToAsync()
        select new ExpandFodderVMCommandResponse
        {
            Config = substitutedConfig,
        };
}
