using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Dbosoft.Rebus.Operations;
using Eryph.Core;
using Eryph.Core.Genetics;
using Eryph.Messages.Resources.Catlets.Commands;
using Eryph.Modules.VmHostAgent.Genetics;
using Eryph.VmManagement;
using LanguageExt;
using LanguageExt.Common;
using Rebus.Extensions;
using Rebus.Handlers;
using Rebus.Pipeline;

namespace Eryph.Modules.VmHostAgent;

using ArchitectureMap = HashMap<GeneIdentifierWithType, Architecture>;

internal class ResolveGenesCommandHandler(
    IMessageContext messageContext,
    ITaskMessaging messaging,
    IFileSystemService fileSystem,
    IHostSettingsProvider hostSettingsProvider,
    IGenePoolFactory genePoolFactory,
    IVmHostAgentConfigurationManager vmHostAgentConfigManager)
    : IHandleMessages<OperationTask<ResolveGenesCommand>>
{
    public Task Handle(OperationTask<ResolveGenesCommand> message) =>
        Handle(message.Command, messageContext.GetCancellationToken())
            .FailOrComplete(messaging, message);

    private EitherAsync<Error, ResolveGenesCommandResponse> Handle(
        ResolveGenesCommand command,
        CancellationToken cancellationToken) =>
        from hostSettings in hostSettingsProvider.GetHostSettings()
        from vmHostAgentConfig in vmHostAgentConfigManager.GetCurrentConfiguration(hostSettings)
        let genePoolPath = GenePoolPaths.GetGenePoolPath(vmHostAgentConfig)
        let genePool = genePoolFactory.CreateLocal(genePoolPath)
        let genePoolReader = new LocalGenepoolReader(fileSystem, vmHostAgentConfig)
        from result in Handle(command, genePool, genePoolPath, cancellationToken)
        select result;

    public static EitherAsync<Error, ResolveGenesCommandResponse> Handle(
        ResolveGenesCommand command,
        ILocalGenePool genePool,
        string genePoolPath,
        CancellationToken cancellationToken) =>
        from result in command.Genes.ToSeq().Fold<EitherAsync<Error, ArchitectureMap>>(
            new ArchitectureMap(),
            (state, geneId) => state.Bind(m => ResolveArchitecture(
                geneId, command.CatletArchitecture, m, genePool, cancellationToken)))
        select new ResolveGenesCommandResponse
        {
            ResolvedGenes = result.Map(v => new UniqueGeneIdentifier(
                v.Key.GeneType, v.Key.GeneIdentifier, v.Value)).ToList(),
        };

    private static EitherAsync<Error, ArchitectureMap> ResolveArchitecture(
        GeneIdentifierWithType geneIdWithType,
        Architecture catletArchitecture,
        ArchitectureMap resolvedArchitectures,
        ILocalGenePool genePool,
        CancellationToken cancellationToken) =>
        resolvedArchitectures.Find(geneIdWithType).Match(
            Some: _ => resolvedArchitectures,
            None: () =>
                from resolvedArchitecture in ResolveArchitecture(geneIdWithType, catletArchitecture, genePool, cancellationToken)
                select resolvedArchitectures.Add(geneIdWithType, resolvedArchitecture));

    private static EitherAsync<Error, Architecture> ResolveArchitecture(
        GeneIdentifierWithType geneIdWithType,
        Architecture catletArchitecture,
        ILocalGenePool genePool,
        CancellationToken cancellationToken) =>
        from manifest in genePool.GetCachedGeneSet(geneIdWithType.GeneIdentifier.GeneSet, cancellationToken)
        from architecture in GeneSetManifestUtils.FindBestArchitecture(
            manifest.MetaData, catletArchitecture, geneIdWithType.GeneType, geneIdWithType.GeneIdentifier.GeneName).ToAsync()
        from validArchitecture in architecture.ToEitherAsync(
            Error.New($"The gene '{geneIdWithType}' is not compatible with the hypervisor and/or processor architecture"))
        select validArchitecture;
}
