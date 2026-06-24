using System;

namespace Eryph.Messages.Resources.Disks;

[SendMessageTo(MessageRecipient.Controllers)]
public class CreateVirtualDiskCommand : IHasCorrelationId, ICommandWithName
{
    public Guid ProjectId { get; set; }

    public string? Name { get; set; }

    public string? Environment { get; set; }

    public string? DataStore { get; set; }

    public string? StorageIdentifier { get; set; }

    public int Size { get; set; }

    public string GetCommandName()
    {
        return $"Create disk {Name}";
    }

    public Guid CorrelationId { get; set; }
}
