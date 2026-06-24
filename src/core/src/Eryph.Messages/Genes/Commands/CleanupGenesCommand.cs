namespace Eryph.Messages.Genes.Commands;

[SendMessageTo(MessageRecipient.Controllers)]
public class CleanupGenesCommand : ICommandWithName
{
    public string GetCommandName() => "Cleanup gene pool";
}
