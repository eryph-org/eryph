using System;
using System.Threading;
using System.Threading.Tasks;
using Dbosoft.Rebus.Operations;
using Eryph.Messages.Genes.Commands;
using Eryph.Modules.GenePool.Genetics;
using JetBrains.Annotations;
using LanguageExt;
using LanguageExt.Common;
using Rebus.Handlers;
using static LanguageExt.Prelude;

namespace Eryph.Modules.GenePool;

[UsedImplicitly]
internal class PrepareGeneCommandHandler(
    ITaskMessaging messaging,
    IGeneRequestDispatcher imageRequestDispatcher)
    : IHandleMessages<OperationTask<PrepareGeneCommand>>
{
    public Task Handle(OperationTask<PrepareGeneCommand> message) =>
        HandleCommand(message)
            .FailOrContinue(messaging, message);

    private EitherAsync<Error, Unit> HandleCommand(
        OperationTask<PrepareGeneCommand> message) =>
        from _ in TryAsync(async () =>
                {
                    await imageRequestDispatcher.NewGeneRequestTask(
                        message, message.Command.Gene, message.Command.Hash);
                    return unit;
                })
            .ToEither()
        select unit;
}
