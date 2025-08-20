using Eryph.Core.Genetics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Eryph.Messages.Resources.Catlets.Commands;

[SendMessageTo(MessageRecipient.Controllers)]
public class ResolveCatletSpecificationCommand
{
    public string ConfigYaml { get; set; }

    public Architecture Architecture { get; set; }

    public string AgentName { get; set; }
}
