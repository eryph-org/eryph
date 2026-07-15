using System;
using System.Collections.Generic;
using Eryph.ConfigModel;
using Eryph.Core.Genetics;
using Eryph.Resources;

namespace Eryph.Messages.Resources.CatletSpecifications;

[SendMessageTo(MessageRecipient.Controllers)]
public class DeployCatletSpecificationCommand : ICommandWithName, IHasCorrelationId, IHasResource
{
    public Guid SpecificationId { get; set; }

    public Guid SpecificationVersionId { get; set; }

    public Architecture? Architecture { get; set; }

    /// <summary>
    /// The environment to deploy into. A specification is project level and deploys into many
    /// environments, so the environment belongs to the deployment, not to the specification.
    /// </summary>
    public EnvironmentName? Environment { get; set; }

    public string? Name { get; set; }

    public bool Redeploy { get; set; }

    public IReadOnlyDictionary<string, string>? Variables { get; set; }

    public string GetCommandName() => $"Deploy catlet specification {Name}";

    public Guid CorrelationId { get; set; }

    public Resource Resource => new(ResourceType.CatletSpecification, SpecificationId);
}
