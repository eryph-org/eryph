using System;
using System.Collections.Generic;
using Eryph.ConfigModel.Catlets;
using Eryph.Core.Genetics;

namespace Eryph.Messages.Resources.Catlets.Commands;

[SendMessageTo(MessageRecipient.Controllers)]
public class DeployCatletCommand : IHasCorrelationId, ICommandWithName
{
    public Guid ProjectId { get; set; }

    public string AgentName { get; set; }

    public Architecture Architecture { get; set; }

    public CatletConfig Config { get; set; }

    public string ConfigYaml { get; set; }

    public IReadOnlyDictionary<UniqueGeneIdentifier, GeneHash> ResolvedGenes { get; set; }

    public Guid CorrelationId { get; set; }

    public Guid? CatletId { get; set; }

    public string GetCommandName() =>
        CatletId.HasValue ? $"Deploy catlet {CatletId.Value}" : "Deploy new catlet"; 
}
