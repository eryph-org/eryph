using Eryph.Resources.Disks;

namespace Eryph.Messages.Resources.Disks;

[SendMessageTo(MessageRecipient.VMHostAgent)]
public class CheckDisksExistsCommand : IHostAgentCommand
{
    public DiskInfo[]? Disks { get; set; }
    public string? AgentName { get; set; }
}
