using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.ConfigModel.Catlets;
using Eryph.Core.Genetics;
using Eryph.Resources.Machines;
using JetBrains.Annotations;

namespace Eryph.Messages.Resources.Catlets.Commands;

[SendMessageTo(MessageRecipient.VMHostAgent)]
public class ExpandFodderVMCommand : IHostAgentCommand
{
    public string AgentName { get; set; }

    [CanBeNull] public CatletMetadata CatletMetadata { get; set; }

    public CatletConfig Config { get; set; }

    public IReadOnlyList<UniqueGeneIdentifier> ResolvedGenes { get; set; }
}
