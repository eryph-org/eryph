using System;

namespace Eryph.Messages.Resources.Networks.Commands;

[SendMessageTo(MessageRecipient.Controllers)]
public class UpdateProjectNetworkPlanCommand
{
    public Guid ProjectId { get; set; }
}
