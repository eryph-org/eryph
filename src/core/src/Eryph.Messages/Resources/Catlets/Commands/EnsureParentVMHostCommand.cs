using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Eryph.Messages.Resources.Catlets.Commands;

[SendMessageTo(MessageRecipient.VMHostAgent)]
public class EnsureParentVMHostCommand : IHostAgentCommand
{
    public string AgentName { get; set; }

    public string ParentId { get; set; }
}
