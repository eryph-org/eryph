using System;

namespace Eryph.Messages.Projects;

[SendMessageTo(MessageRecipient.Controllers)]
public class AddProjectMemberCommand : IHasCorrelationId, IHasProjectName
{
    public string MemberId { get; set; }
    public Guid CorrelationId { get; set; }

    public Guid TenantId { get; set; }

    public string ProjectName { get; set; }

    public Guid RoleId { get; set; }

}