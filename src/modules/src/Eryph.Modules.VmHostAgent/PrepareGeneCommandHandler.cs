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
        from _ in TryAsync(async () =>
                {
                    await imageRequestDispatcher.NewGeneRequestTask(
                        message, message.Command.Gene);
                    return unit;
                })
            .ToEither()
        select unit;
}