using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.ConfigModel.Catlets;
using Eryph.Core.Genetics;
using LanguageExt;

namespace Eryph.Messages.Resources.Catlets.Commands;

[SendMessageTo(MessageRecipient.Controllers)]
public class DeployCatletCommand : IHasCorrelationId, ICommandWithName
{
    public Guid TenantId { get; set; }

    public string AgentName { get; set; }

    public Architecture Architecture { get; set; }

    public CatletConfig Config { get; set; }

    public IReadOnlyDictionary<UniqueGeneIdentifier, GeneHash> ResolvedGenes { get; set; }

    public Guid CorrelationId { get; set; }

    public Guid? CatletId { get; set; }

    public string GetCommandName() =>
        CatletId.HasValue ? $"Deploy catlet {CatletId.Value}" : "Deploy new catlet"; 
}
