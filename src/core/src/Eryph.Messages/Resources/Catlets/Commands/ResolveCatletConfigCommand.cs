using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.ConfigModel.Catlets;

namespace Eryph.Messages.Resources.Catlets.Commands;

[SendMessageTo(MessageRecipient.VMHostAgent)]
public class ResolveCatletConfigCommand : IHostAgentCommand
{
    public string AgentName { get; set; }

    public CatletConfig Config { get; set; }
}