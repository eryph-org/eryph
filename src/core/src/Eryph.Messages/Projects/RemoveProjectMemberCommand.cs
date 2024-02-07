using System;

namespace Eryph.Messages.Projects;

[SendMessageTo(MessageRecipient.Controllers)]
public class RemoveProjectMemberCommand : IHasCorrelationId, IHasProjectId
{
    public Guid ProjectId { get; set; }
    public Guid CorrelationId { get; set; }
    public Guid AssignmentId { get; set; }
    public string CurrentIdentityId { get; set; }

}