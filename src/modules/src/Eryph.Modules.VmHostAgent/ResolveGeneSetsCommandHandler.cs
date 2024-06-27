using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dbosoft.Rebus.Operations;
using Eryph.ConfigModel.Catlets;
using Eryph.Core;
using Eryph.Messages.Resources.Genes.Commands;
using Eryph.Modules.VmHostAgent.Genetics;
using Eryph.VmManagement;
using JetBrains.Annotations;
using LanguageExt;
using LanguageExt.Common;
using Rebus.Handlers;

using static LanguageExt.Prelude;

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
        from genesets in CatletBreeding.CollectGeneSetsRecursively(
            catletConfig, genepoolReader)
            .ToEither()
            .MapLeft(errors => Error.New("Some gene identifiers are invalid.", Error.Many(errors)))
            .ToAsync()
        from _ in genesets.Map(geneSetId => geneProvider.ResolveGeneSet(
                geneSetId, (_, _) => Task.FromResult(unit), default))
            .SequenceSerial()
        select unit;
}
