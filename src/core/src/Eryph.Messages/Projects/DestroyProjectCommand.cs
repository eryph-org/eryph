using System;

namespace Eryph.Messages.Projects;

[SendMessageTo(MessageRecipient.Controllers)]
public class DestroyProjectCommand : IHasCorrelationId, IHasProjectId
{
    public Guid CorrelationId { get; set; }
    public Guid ProjectId { get; set; }
}
