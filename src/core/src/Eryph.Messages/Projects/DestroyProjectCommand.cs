using System;

namespace Eryph.Messages.Projects;

[SendMessageTo(MessageRecipient.Controllers)]
public class DestroyProjectCommand : IHasCorrelationId, IHasProjectId
{
    public Guid ProjectId { get; set; }
    public Guid CorrelationId { get; set; }
}