using System;

namespace Eryph.Messages.Projects;

[SendMessageTo(MessageRecipient.Controllers)]
public class UpdateProjectCommand : IHasCorrelationId, IHasProjectId
{
    public Guid ProjectId { get; set; }
    public string Name { get; set; }
    public Guid CorrelationId { get; set; }
}