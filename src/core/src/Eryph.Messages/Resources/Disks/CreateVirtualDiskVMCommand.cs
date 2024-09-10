using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.ConfigModel;
using Eryph.Resources;

namespace Eryph.Messages.Resources.Disks;

[SendMessageTo(MessageRecipient.VMHostAgent)]
public class CreateVirtualDiskVMCommand : IHasResource, IHostAgentCommand
{
    [PrivateIdentifier]
    public string AgentName { get; set; }

    public Guid DiskId { get; set; }

    public int Size { get; set; }

    public CatletDriveName Name { get; set; }

    public ProjectName ProjectName { get; set; }

    public EnvironmentName Environment { get; set; }

    public DataStoreName DataStore { get; set; }

    public StorageIdentifier StorageIdentifier { get; set; }

    public Resource Resource => new(ResourceType.VirtualDisk, DiskId);
}
