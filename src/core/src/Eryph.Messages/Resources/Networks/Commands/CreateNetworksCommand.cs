using System;
using Eryph.ConfigModel.Networks;

namespace Eryph.Messages.Resources.Networks.Commands;

[SendMessageTo(MessageRecipient.Controllers)]
public class CreateNetworksCommand : IHasCorrelationId, IHasProjectId
{
    public ProjectNetworksConfig Config { get; set; }
        
    public Guid CorrelationId { get; set; }
        
    public Guid TenantId { get; set; }

    public Guid ProjectId { get; set; }
}
