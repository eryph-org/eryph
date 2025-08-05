using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Dbosoft.Rebus.Operations;
using Eryph.Core;
using Eryph.Core.Sys;
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
    ILocalGenePool localGenePool)
    : IHandleMessages<OperationTask<RemoveGenesVmHostCommand>>
{
    public Task Handle(OperationTask<RemoveGenesVmHostCommand> message) =>
        Handle(message.Command).RunWithCancel(CancellationToken.None).FailOrComplete(messaging, message);

    public Aff<CancelRt, Unit> Handle(RemoveGenesVmHostCommand command) =>
        from _ in command.Genes.ToSeq()
            .Map(localGenePool.RemoveCachedGene)
            .SequenceSerial()
        select unit;
}
