using System;
using Eryph.ConfigModel.Catlets;
using Eryph.Resources;

namespace Eryph.Messages.Resources.Catlets.Commands;

[SendMessageTo(MessageRecipient.Controllers)]
public class UpdateCatletNetworksCommand : IHasResource, IHasProjectId
{
    public CatletConfig? Config { get; set; }

    public Guid CatletId { get; set; }

    /// <summary>
    /// The site the catlet is pinned to, as decided when it was placed. Passed rather than looked
    /// up: it is the catlet's own site that decides which networks it can reach, and re-deriving it
    /// from the catlet's environment would answer with where a new catlet would go instead.
    /// </summary>
    public Guid SiteId { get; set; }

    public Guid CatletMetadataId { get; set; }
    public Guid ProjectId { get; set; }

    public Resource Resource => new(ResourceType.Catlet, CatletId);
}
