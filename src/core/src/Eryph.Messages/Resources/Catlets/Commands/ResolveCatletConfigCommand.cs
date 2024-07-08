using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.ConfigModel.Catlets;

namespace Eryph.Messages.Resources.Catlets.Commands;

/// <summary>
/// This command asks the VMHostAgent to resolve the ancestors and all
/// referenced gene sets of the included <see cref="CatletConfig"/>.
/// The VM host agent will send a <see cref="ResolveCatletConfigCommandResponse"/>.
/// </summary>
/// <remarks>
/// This command bundles all necessary resolving. This way, we limit the
/// number of messages which we need to exchange with the VM host agent.
/// </remarks>
[SendMessageTo(MessageRecipient.VMHostAgent)]
public class ResolveCatletConfigCommand : IHostAgentCommand
{
    public string AgentName { get; set; }

    public CatletConfig Config { get; set; }
}
