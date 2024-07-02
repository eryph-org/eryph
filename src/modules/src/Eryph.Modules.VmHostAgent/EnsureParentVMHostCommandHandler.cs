using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dbosoft.Rebus.Operations;
using Eryph.ConfigModel;
using Eryph.ConfigModel.Catlets;
using Eryph.ConfigModel.Json;
using Eryph.Core;
using Eryph.GenePool.Model;
using Eryph.Messages.Resources.Catlets.Commands;
using Eryph.Modules.VmHostAgent.Genetics;
using Eryph.VmManagement;
using JetBrains.Annotations;
using LanguageExt;
using LanguageExt.Common;
using Rebus.Handlers;

using static LanguageExt.Prelude;

namespace Eryph.Modules.VmHostAgent;

[UsedImplicitly]
internal class EnsureParentVMHostCommandHandler(
    ITaskMessaging messaging,
    IHostSettingsProvider hostSettingsProvider,
    IVmHostAgentConfigurationManager vmHostAgentConfigManager,
    IGeneProvider geneProvider)
    : IHandleMessages<OperationTask<EnsureParentVMHostCommand>>
{
    public Task Handle(OperationTask<EnsureParentVMHostCommand> message) =>
        ResolveGeneSets(message.Command.ParentId)
            .FailOrComplete(messaging, message);

    private EitherAsync<Error, EnsureParentVMHostCommandResponse> ResolveGeneSets(
        string parentId) =>
        from validParentId in Optional(parentId).Filter(notEmpty)
            .ToEither(Error.New("The parent ID is empty"))
            .Bind(GeneSetIdentifier.NewEither)
            .ToAsync()
        from hostSettings in hostSettingsProvider.GetHostSettings()
        from vmHostAgentConfig in vmHostAgentConfigManager.GetCurrentConfiguration(hostSettings)
        from provideResult in geneProvider.ProvideGene(
            GeneType.Catlet,
            new GeneIdentifier(validParentId, GeneName.New("catlet")),
            (s1, i) => Task.FromResult(unit),
            default)
        from a in guard(provideResult.RequestedGene == provideResult.ResolvedGene,
            Error.New("The resolved gene is different. This code must only be called with resolved IDs. "
                + $"Requested: {provideResult.RequestedGene}; Resolved: {provideResult.ResolvedGene}"))
        let genepoolReader = new LocalGenepoolReader(vmHostAgentConfig)
        from parentConfig in CatletGeneResolving.ReadCatletConfig(validParentId, genepoolReader)
            .ToAsync()
        select new EnsureParentVMHostCommandResponse()
        {
            Config = parentConfig,
            ParentId = parentId,
        };
}
