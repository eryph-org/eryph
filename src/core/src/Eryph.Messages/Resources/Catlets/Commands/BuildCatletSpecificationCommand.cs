using Eryph.Core.Genetics;

namespace Eryph.Messages.Resources.Catlets.Commands;

[SendMessageTo(MessageRecipient.Controllers)]
public class BuildCatletSpecificationCommand
{
    public string ContentType { get; set; }

    public string Configuration { get; set; }

    public Architecture Architecture { get; set; }

    public string AgentName { get; set; }
}
