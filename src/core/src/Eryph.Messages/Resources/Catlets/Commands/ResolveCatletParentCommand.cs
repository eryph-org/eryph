using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Eryph.Messages.Resources.Catlets.Commands;

[SendMessageTo(MessageRecipient.Controllers)]
public class ResolveCatletParentCommand
{
    public string AgentName { get; init; }

    public string ParentId { get; init; }

    public string[] Chain { get; set; }
}
