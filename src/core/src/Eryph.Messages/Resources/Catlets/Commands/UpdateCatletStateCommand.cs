using System;
using Eryph.Resources;
using Eryph.Resources.Machines;

namespace Eryph.Messages.Resources.Catlets.Commands;

[SendMessageTo(MessageRecipient.Controllers)]
public class UpdateCatletStateCommand : IHasResource
{
    public Guid CatletId { get; set; }

    public Guid VmId { get; set; }

    public VmStatus Status { get; set; }

    public TimeSpan UpTime { get; set; }

    public DateTimeOffset Timestamp { get; set; }

    public Resource Resource => new(ResourceType.Catlet, CatletId);
}
