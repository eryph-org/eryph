using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dbosoft.Rebus.Operations;
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

/// <summary>
/// This handler removes the genes specified in the <see cref="RemoveGenesVMCommand"/>
/// from the local gene pool.
/// </summary>
[UsedImplicitly]
internal class RemoveGenesVMCommandHandler(
    ITaskMessaging messaging,
    IHostSettingsProvider hostSettingsProvider,
    IVmHostAgentConfigurationManager vmHostAgentConfigManager,
    IGenePoolFactory genePoolFactory)
    : IHandleMessages<OperationTask<RemoveGenesVMCommand>>
{
    public Task Handle(OperationTask<RemoveGenesVMCommand> message) =>
        Handle(message.Command).FailOrComplete(messaging, message);

    public EitherAsync<Error, Unit> Handle(RemoveGenesVMCommand command) =>
        from hostSettings in hostSettingsProvider.GetHostSettings()
        from vmHostAgentConfig in vmHostAgentConfigManager.GetCurrentConfiguration(hostSettings)
        let genePoolPath = GenePoolPaths.GetGenePoolPath(vmHostAgentConfig)
        let genePool = genePoolFactory.CreateLocal()
        from _ in command.Genes.ToSeq()
            .Map(gene => genePool.RemoveCachedGene(genePoolPath, gene.GeneType, gene.GeneIdentifier))
            .SequenceSerial()
        select unit;
}
