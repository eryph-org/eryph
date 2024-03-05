using System;

namespace Eryph.Messages.Projects;

[SendMessageTo(MessageRecipient.Controllers)]
public class CreateProjectCommand : IHasCorrelationId, IHasProjectName
{
    public string ProjectName { get; set; }
    public Guid CorrelationId { get; set; }

    public bool NoDefaultNetwork { get; set; }
    public string IdentityId { get; set; }
    public Guid TenantId { get; set; }
}