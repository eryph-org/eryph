using System;
using Eryph.ConfigModel.Catlets;

namespace Eryph.Messages.Resources.Catlets.Commands
{
    [SendMessageTo(MessageRecipient.Controllers)]
    public class CreateCatletCommand : IHasCorrelationId, ICommandWithName
    {
        public CatletConfig Config { get; set; }
        public Guid CorrelationId { get; set; }
        public string GetCommandName()
        {
            var catletName = Config?.Name ?? "Catlet";
            return $"Create catlet {catletName}";
        }
    }
}