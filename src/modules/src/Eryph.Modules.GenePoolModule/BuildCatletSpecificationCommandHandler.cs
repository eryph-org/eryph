using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Dbosoft.Rebus.Operations;
using Eryph.Messages.Resources.CatletSpecifications;
using Eryph.VmManagement;
using JetBrains.Annotations;
using LanguageExt;
using LanguageExt.Common;
using Rebus.Handlers;

namespace Eryph.Modules.GenePool;

[UsedImplicitly]
internal class BuildCatletSpecificationCommandHandler(
    ITaskMessaging messaging,
    IGenePoolReader genePoolReader)
    : IHandleMessages<OperationTask<BuildCatletSpecificationGenePoolCommand>>
{
    public Task Handle(OperationTask<BuildCatletSpecificationGenePoolCommand> message) =>
        HandleCommand(message.Command).FailOrComplete(messaging, message);

    private EitherAsync<Error, BuildCatletSpecificationGenePoolCommandResponse> HandleCommand(
        BuildCatletSpecificationGenePoolCommand genePoolCommand) =>
        from result in CatletSpecificationBuilder.Build(
            genePoolCommand.CatletConfig,
            genePoolCommand.CatletArchitecture,
            genePoolReader,
            CancellationToken.None)
        select new BuildCatletSpecificationGenePoolCommandResponse
        {
            BuiltConfig = result.ExpandedConfig,
            ResolvedGenes = result.ResolvedGenes.ToDictionary(),
        };
}
