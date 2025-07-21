using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.ConfigModel.Catlets;
using Eryph.Core.Genetics;

namespace Eryph.Messages.Resources.Catlets.Commands;

[SendMessageTo(MessageRecipient.GenepoolAgent)]
public class ResolveCatletConfigCommand : IGenepoolAgentCommand
{
    public string AgentName { get; set; }

    public Architecture CatletArchitecture { get; set; }

    public CatletConfig Config { get; set; }
}
