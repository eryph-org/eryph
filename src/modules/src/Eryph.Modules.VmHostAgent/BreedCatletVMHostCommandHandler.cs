
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dbosoft.Rebus.Operations;
using Eryph.ConfigModel;
using Eryph.ConfigModel.Catlets;
using Eryph.Core;
using Eryph.Messages.Resources.Catlets.Commands;
using Eryph.VmManagement;
using JetBrains.Annotations;
using LanguageExt;
using LanguageExt.Common;
using Rebus.Handlers;

using static LanguageExt.Prelude;

namespace Eryph.Modules.VmHostAgent;

[UsedImplicitly]
internal class BreedCatletVMHostCommandHandler(
    ITaskMessaging messaging,
    IHostSettingsProvider hostSettingsProvider,
    IVmHostAgentConfigurationManager vmHostAgentConfigManager)
    : IHandleMessages<OperationTask<BreedCatletVMHostCommand>>
{
    public Task Handle(OperationTask<BreedCatletVMHostCommand> message) =>
        ResolveGeneSets(message.Command.Config)
            .FailOrComplete(messaging, message);

    private EitherAsync<Error, BreedCatletVMHostCommandResponse> ResolveGeneSets(
        CatletConfig catletConfig) =>
        from hostSettings in hostSettingsProvider.GetHostSettings()
        from vmHostAgentConfig in vmHostAgentConfigManager.GetCurrentConfiguration(hostSettings)
        let genepoolReader = new LocalGenepoolReader(vmHostAgentConfig)
        from optionalParentId in Optional(catletConfig.Parent)
            .Filter(notEmpty)
            .Map(GeneSetIdentifier.NewEither)
            .Sequence()
            .ToAsync()
        from optionalParentConfig in optionalParentId
            .Map(pId => CatletBreeding.Breed(pId, genepoolReader))
            .Sequence()
            .ToAsync()
        from breedConfig in CatletBreeding.Breed(optionalParentConfig, catletConfig, genepoolReader)
            .ToAsync()
        select new BreedCatletVMHostCommandResponse()
        {
            BreedConfig = breedConfig,
            ParentConfig = optionalParentConfig.IfNoneUnsafe((CatletConfig)null),
        };
}