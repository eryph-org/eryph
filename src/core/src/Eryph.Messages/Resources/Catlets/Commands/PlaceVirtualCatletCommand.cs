using Eryph.ConfigModel.Catlets;

namespace Eryph.Messages.Resources.Catlets.Commands
{
    [SendMessageTo(MessageRecipient.Controllers)]
    public class PlaceVirtualCatletCommand
    {
        public CatletConfig Config { get; set; }
    }
}