using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.ConfigModel.Catlets;
using Eryph.Core.Genetics;

namespace Eryph.Messages.Resources.CatletSpecifications;

[SendMessageTo(MessageRecipient.GenePoolAgent)]
public class BuildCatletSpecificationCommand : IGenePoolAgentCommand, ICommandWithName
{
    public CatletConfig CatletConfig { get; set; }

    public Architecture CatletArchitecture { get; set; }

    public string AgentName { get; set; }

    public string GetCommandName() => "Build catlet specification";
}
