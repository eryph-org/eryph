using System;

namespace Eryph.Messages.Projects;

[SendMessageTo(MessageRecipient.Controllers)]
public class CreateProjectCommand : IHasCorrelationId
{
    public string Name { get; set; }
    public Guid CorrelationId { get; set; }

    public bool NoDefaultNetwork { get; set; }
}