using System;
using System.Collections.Generic;
using Eryph.ConfigModel.Catlets;
using Eryph.Core.Genetics;

namespace Eryph.Messages.Resources.Catlets.Commands;

[SendMessageTo(MessageRecipient.Controllers)]
public class DeployCatletCommand : IHasCorrelationId, ICommandWithName
{
    public Guid ProjectId { get; set; }

    /// <summary>
    /// The site realizing the catlet's environment, resolved by the calling saga when it placed the
    /// catlet. It is pinned on the catlet as-is: the site of a resource must never be re-derived from
    /// its environment, or re-authoring the environment configuration would relocate it.
    /// </summary>
    public Guid SiteId { get; set; }

    public string? AgentName { get; set; }

    public Architecture? Architecture { get; set; }

    public CatletConfig? Config { get; set; }

    public string? ContentType { get; set; }

    public string? OriginalConfig { get; set; }

    public IReadOnlyDictionary<UniqueGeneIdentifier, GeneHash>? ResolvedGenes { get; set; }

    public Guid? CatletId { get; set; }

    public Guid? SpecificationId { get; set; }

    public Guid? SpecificationVersionId { get; set; }

    public string GetCommandName() =>
        CatletId.HasValue ? $"Deploy catlet {CatletId.Value}" : "Deploy new catlet";

    public Guid CorrelationId { get; set; }
}
