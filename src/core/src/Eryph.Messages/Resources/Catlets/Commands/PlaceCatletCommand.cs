using Eryph.ConfigModel.Catlets;

namespace Eryph.Messages.Resources.Catlets.Commands
{
    [SendMessageTo(MessageRecipient.Controllers)]
    public class PlaceCatletCommand
    {
        public CatletConfig Config { get; set; }
    }
}