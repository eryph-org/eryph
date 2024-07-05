using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dbosoft.Rebus.Operations;
using Eryph.ConfigModel;
using Eryph.ConfigModel.Catlets;
using Eryph.Core;
using Eryph.Core.Genetics;
using Eryph.Messages.Resources.Genes.Commands;
using Eryph.Modules.VmHostAgent.Genetics;
using Eryph.VmManagement;
using JetBrains.Annotations;
using LanguageExt;
using LanguageExt.Common;
using Rebus.Handlers;

using static LanguageExt.Prelude;
using CatletGeneResolving = Eryph.VmManagement.CatletGeneResolving;

namespace Eryph.Modules.VmHostAgent;

[UsedImplicitly]
internal class ResolveGeneSetsCommandHandler(
    ITaskMessaging messaging,
    IHostSettingsProvider hostSettingsProvider,
    IVmHostAgentConfigurationManager vmHostAgentConfigManager,
    IGeneProvider geneProvider)
    : IHandleMessages<OperationTask<ResolveGeneSetsCommand>>
{
    public Task Handle(OperationTask<ResolveGeneSetsCommand> message) =>
        ResolveGeneSets(message.Command.Config)
            .FailOrComplete(messaging, message);

    private EitherAsync<Error, Unit> ResolveGeneSets(CatletConfig catletConfig) =>
        from hostSettings in hostSettingsProvider.GetHostSettings()
        from vmHostAgentConfig in vmHostAgentConfigManager.GetCurrentConfiguration(hostSettings)
        let genepoolReader = new LocalGenepoolReader(vmHostAgentConfig)
        from genesets in CollectGeneSetsRecursively(catletConfig, genepoolReader)
            .ToEither()
            .MapLeft(errors => Error.New("Some gene identifiers are invalid.", Error.Many(errors)))
            .ToAsync()
        from _ in genesets.Map(geneSetId => geneProvider.ResolveGeneSet(
                geneSetId, (_, _) => Task.FromResult(unit), default))
            .SequenceSerial()
        select unit;


    public static Validation<Error, Seq<GeneSetIdentifier>> CollectGeneSetsRecursively(
        CatletConfig catletConfig,
        ILocalGenepoolReader genepoolReader) =>
        from optionalParent in Optional(catletConfig.Parent)
            .Filter(notEmpty)
            .Map(GeneSetIdentifier.NewValidation)
            .Sequence()
        from parentGeneSets in optionalParent.Map(parentId =>
                from resolvedParentId in CatletGeneResolving.ResolveGeneSetIdentifier(parentId, genepoolReader)
                    .ToValidation()
                from parentConfig in CatletGeneResolving.ReadCatletConfig(resolvedParentId, genepoolReader)
                    .ToValidation()
                from parentGeneSets in CollectGeneSetsRecursively(parentConfig, genepoolReader)
                select parentGeneSets)
            .Sequence()
            .Map(r => r.ToSeq().Flatten())
        from geneSets in CatletGeneCollecting.CollectGenes(catletConfig)
            .MapT(geneId => geneId.GeneIdentifier.GeneSet)
            .Map(l => l.Distinct())
        select parentGeneSets.Append(geneSets);
}
