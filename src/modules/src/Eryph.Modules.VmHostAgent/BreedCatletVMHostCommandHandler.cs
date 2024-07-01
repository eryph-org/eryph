
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
        from resolvedOptionalParentId in optionalParentId
            .Map(id => genepoolReader.GetGenesetReference(id).Map(r => r | optionalParentId))
            .Sequence()
            .ToAsync()
        from optionalParentConfig in optionalParentId
            .Map(pId => CatletBreeding.BreedRecursively(pId, genepoolReader))
            .Sequence()
            .ToAsync()
        from resolvedConfig in CatletBreeding.ResolveGenesetIdentifiers(catletConfig, genepoolReader)
            .ToAsync()
        let breedConfigResult = from pId in optionalParentId
                                from pConfig in optionalParentConfig
                                select CatletBreeding.Breed(pConfig, pId, catletConfig, genepoolReader)
        from breedConfig in breedConfigResult.Sequence().ToAsync()
        select new BreedCatletVMHostCommandResponse()
        {
            BreedConfig = breedConfig.IfNone(resolvedConfig),
            ParentConfig = optionalParentConfig.IfNoneUnsafe((CatletConfig)null),
        };
}