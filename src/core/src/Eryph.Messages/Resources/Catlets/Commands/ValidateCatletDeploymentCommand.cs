using System;
using System.Collections.Generic;
using Eryph.ConfigModel.Catlets;
using Eryph.Core.Genetics;

namespace Eryph.Messages.Resources.Catlets.Commands;

[SendMessageTo(MessageRecipient.Controllers)]

public class ValidateCatletDeploymentCommand : IHasCorrelationId, ICommandWithName
{
    public Guid ProjectId { get; set; }

    public string AgentName { get; set; }

    public Architecture Architecture { get; set; }

    public CatletConfig Config { get; set; }

    public IReadOnlyDictionary<UniqueGeneIdentifier, GeneHash> ResolvedGenes { get; set; }
    
    public Guid CorrelationId { get; set; }

    public string GetCommandName() => "Validate catlet deployment";
}
