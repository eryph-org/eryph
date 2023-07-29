using System;

namespace Eryph.Messages.Resources.Networks.Commands;

[SendMessageTo(MessageRecipient.Controllers)]
public class UpdateProjectNetworkPlanCommand: IHasProjectId
{
    public Guid ProjectId { get; set; }
}
