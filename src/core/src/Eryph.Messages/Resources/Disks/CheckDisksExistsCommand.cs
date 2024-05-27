using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.Resources.Disks;

namespace Eryph.Messages.Resources.Disks
{
    [SendMessageTo(MessageRecipient.VMHostAgent)]
    public class CheckDisksExistsCommand : IHostAgentCommand
    {
        public string AgentName { get; set; }
        public DiskInfo[] Disks { get; set; }
    }
}
