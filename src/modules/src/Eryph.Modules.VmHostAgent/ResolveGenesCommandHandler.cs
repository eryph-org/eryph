using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dbosoft.Rebus.Operations;
using Eryph.ConfigModel;
using Eryph.ConfigModel.Catlets;
using Eryph.Core;
using Eryph.Genetics;
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
internal class ResolveGenesCommandHandler(
    ITaskMessaging messaging,
    IHostSettingsProvider hostSettingsProvider,
    IVmHostAgentConfigurationManager vmHostAgentConfigManager,
    IGeneProvider geneProvider)
    : IHandleMessages<OperationTask<ResolveGenesCommand>>
{
    public Task Handle(OperationTask<ResolveGenesCommand> message) =>
        ResolveGenes(message.Command.Config)
            .FailOrComplete(messaging, message);

    private EitherAsync<Error, ResolveGenesCommandResponse> ResolveGenes(
        CatletConfig catletConfig) =>
        from hostSettings in hostSettingsProvider.GetHostSettings()
        from vmHostAgentConfig in vmHostAgentConfigManager.GetCurrentConfiguration(hostSettings)
        let genepoolReader = new LocalGenepoolReader(vmHostAgentConfig)
        from geneIds in CatletGeneCollecting.CollectGenes(catletConfig)
            .ToEither()
            .MapLeft(errors => Error.New("Some gene identifiers are invalid.", Error.Many(errors)))
            .ToAsync()
        let geneSetIds = geneIds.Map(geneId => geneId.GeneIdentifier.GeneSet)
            .Distinct()
        from _ in geneSetIds.Map(geneSetId => geneProvider.ResolveGeneSet(
                geneSetId, (_, _) => Task.FromResult(unit), default))
            .SequenceSerial()
        from resolvedConfig in CatletGeneResolving.ResolveGenesetIdentifiers(catletConfig, genepoolReader)
            .ToAsync()
        select new ResolveGenesCommandResponse()
        {
            Config = resolvedConfig,
        };
}
