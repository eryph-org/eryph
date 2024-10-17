using Dbosoft.Rebus.Operations;
using Eryph.Messages.Resources.Catlets.Commands;
using Rebus.Handlers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LanguageExt;
using LanguageExt.Common;
using Rebus.Extensions;
using Rebus.Pipeline;
using Eryph.Core;
using Eryph.Modules.VmHostAgent.Genetics;
using Eryph.VmManagement;
using Eryph.ConfigModel;
using Eryph.Core.Genetics;

namespace Eryph.Modules.VmHostAgent;

using ArchitectureMap = HashMap<GeneIdentifier, GeneArchitecture>;

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
        let genePool = genePoolFactory.CreateLocal()
        let genePoolPath = GenePoolPaths.GetGenePoolPath(vmHostAgentConfig)
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
            (state, geneId) => state.Bind(m => ResolveArchitecture(geneId, m, genePool, genePoolPath, cancellationToken)))
        select new ResolveGenesCommandResponse
        {
            ResolvedArchitectures = result.ToDictionary(),
        };

    private static EitherAsync<Error, ArchitectureMap> ResolveArchitecture(
        GeneIdentifier geneId,
        ArchitectureMap resolvedArchitectures,
        ILocalGenePool genePool,
        string genePoolPath,
        CancellationToken cancellationToken) =>
        resolvedArchitectures.Find(geneId).Match(
            Some: _ => resolvedArchitectures,
            None: () =>
                from resolvedArchitecture in ResolveArchitecture(geneId, genePool, genePoolPath, cancellationToken)
                select resolvedArchitectures.Add(geneId, resolvedArchitecture));

    private static EitherAsync<Error, GeneArchitecture> ResolveArchitecture(
        GeneIdentifier geneId,
        ILocalGenePool genePool,
        string genePoolPath,
        CancellationToken cancellationToken) =>
        from manifest in genePool.GetCachedGeneSet(genePoolPath, geneId.GeneSet, cancellationToken)
        let catletArchitecture = GeneArchitecture.New("hyperv/amd64")
        from architecture in GeneSetManifestUtils.FindBestArchitecture(
            manifest.MetaData, catletArchitecture, geneId.GeneName).ToAsync()
        from validArchitecture in architecture.ToEitherAsync(
            Error.New($"The gene '{geneId}' is not compatible with the hypervisor and/or processor architecture"))
        select validArchitecture;
}
