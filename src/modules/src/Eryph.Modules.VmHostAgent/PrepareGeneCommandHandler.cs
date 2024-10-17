using System;
using System.Threading;
using System.Threading.Tasks;
using Dbosoft.Rebus.Operations;
using Eryph.ConfigModel;
using Eryph.Core;
using Eryph.Core.Genetics;
using Eryph.Messages.Genes.Commands;
using Eryph.Modules.VmHostAgent.Genetics;
using Eryph.VmManagement;
using JetBrains.Annotations;
using LanguageExt;
using LanguageExt.Common;
using Microsoft.Extensions.Logging;
using Rebus.Extensions;
using Rebus.Handlers;
using Rebus.Pipeline;
using static LanguageExt.Prelude;

namespace Eryph.Modules.VmHostAgent;

[UsedImplicitly]
internal class PrepareGeneCommandHandler(
    IMessageContext messageContext,
    ITaskMessaging messaging,
    IGeneRequestDispatcher imageRequestDispatcher,
    IFileSystemService fileSystem,
    IHostSettingsProvider hostSettingsProvider,
    IGenePoolFactory genePoolFactory,
    IVmHostAgentConfigurationManager vmHostAgentConfigManager)
    : IHandleMessages<OperationTask<PrepareGeneCommand>>
{
    public Task Handle(OperationTask<PrepareGeneCommand> message) =>
        HandleCommand(message)
            .FailOrContinue(messaging, message);

    private EitherAsync<Error, Unit> HandleCommand(
        OperationTask<PrepareGeneCommand> message) =>
        from uniqueGeneId in ResolveGene(message.Command.GeneIdentifier)
        from _ in TryAsync(() => imageRequestDispatcher.NewGeneRequestTask(
                message, uniqueGeneId))
            .ToEither()
        select unit;

    private EitherAsync<Error, UniqueGeneIdentifier> ResolveGene(
        GeneIdentifierWithType geneIdWithType) =>
        from hostSettings in hostSettingsProvider.GetHostSettings()
        from vmHostAgentConfig in vmHostAgentConfigManager.GetCurrentConfiguration(hostSettings)
        let genePool = genePoolFactory.CreateLocal()
        let genePoolPath = GenePoolPaths.GetGenePoolPath(vmHostAgentConfig)
        let genePoolReader = new LocalGenepoolReader(fileSystem, vmHostAgentConfig)
        from architecture in ResolveArchitecture(geneIdWithType, genePool, genePoolPath, default)
        select new UniqueGeneIdentifier(geneIdWithType.GeneType, geneIdWithType.GeneIdentifier, architecture);

    private static EitherAsync<Error, GeneArchitecture> ResolveArchitecture(
        GeneIdentifierWithType geneId,
        ILocalGenePool genePool,
        string genePoolPath,
        CancellationToken cancellationToken) =>
        from manifest in genePool.GetCachedGeneSet(genePoolPath, geneId.GeneIdentifier.GeneSet, cancellationToken)
        let catletArchitecture = GeneArchitecture.New("hyperv/amd64")
        from architecture in GeneSetManifestUtils.FindBestArchitecture(
            manifest.MetaData, geneId.GeneType, catletArchitecture, geneId.GeneIdentifier.GeneName).ToAsync()
        from validArchitecture in architecture.ToEitherAsync(
            Error.New($"The gene '{geneId}' is not compatible with the hypervisor and/or processor architecture"))
        select validArchitecture;
}