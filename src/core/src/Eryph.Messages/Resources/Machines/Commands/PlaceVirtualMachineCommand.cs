using Eryph.ConfigModel.Catlets;

namespace Eryph.Messages.Resources.Machines.Commands
{
    [SendMessageTo(MessageRecipient.Controllers)]
    public class PlaceVirtualMachineCommand
    {
        public CatletConfig Config { get; set; }
    }
}