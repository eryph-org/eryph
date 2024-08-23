using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Eryph.Messages.Resources.Disks;

[SendMessageTo(MessageRecipient.Controllers)]
public class CreateVirtualDiskCommand : IHasCorrelationId, ICommandWithName
{
    public Guid TenantId { get; set; }
    
    public Guid ProjectId { get; set; }

    public Guid CorrelationId { get; set; }

    public string Name { get; set; }

    public string Environment { get; set; }

    public string DataStore { get; set; }

    public string StorageIdentifier { get; set; }

    public int Size { get; set; }

    public string GetCommandName()
    {
        return $"Create disk {Name}";
    }
}
