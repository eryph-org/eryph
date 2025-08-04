using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dbosoft.Rebus.Operations;
using Eryph.Core;
using Eryph.Core.VmAgent;
using Eryph.Messages.Genes.Commands;
using Eryph.Modules.GenePool.Genetics;
using Eryph.VmManagement;
using JetBrains.Annotations;
using LanguageExt;
using LanguageExt.Common;
using Rebus.Handlers;

using static LanguageExt.Prelude;

namespace Eryph.Modules.GenePool;

/// <summary>
/// This handler removes the genes specified in the <see cref="RemoveGenesVmHostCommand"/>
/// from the local gene pool.
/// </summary>
[UsedImplicitly]
internal class RemoveGenesVmHostCommandHandler(
    ITaskMessaging messaging,
    IHostSettingsProvider hostSettingsProvider,
    IVmHostAgentConfigurationManager vmHostAgentConfigManager,
    IGenePoolFactory genePoolFactory)
    : IHandleMessages<OperationTask<RemoveGenesVmHostCommand>>
{
    public Task Handle(OperationTask<RemoveGenesVmHostCommand> message) =>
        Handle(message.Command).FailOrComplete(messaging, message);

    public EitherAsync<Error, Unit> Handle(RemoveGenesVmHostCommand command) =>
        from hostSettings in hostSettingsProvider.GetHostSettings()
        from vmHostAgentConfig in vmHostAgentConfigManager.GetCurrentConfiguration(hostSettings)
        let genePoolPath = GenePoolPaths.GetGenePoolPath(vmHostAgentConfig)
        let genePool = genePoolFactory.CreateLocal(genePoolPath)
        from _ in command.Genes.ToSeq()
            .Map(genePool.RemoveCachedGene)
            .SequenceSerial()
        select unit;
}
