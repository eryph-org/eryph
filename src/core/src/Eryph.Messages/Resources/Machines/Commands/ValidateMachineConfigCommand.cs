using System;
using Eryph.ConfigModel.Catlets;


namespace Eryph.Messages.Resources.Machines.Commands
{
    [SendMessageTo(MessageRecipient.Controllers)]
    public class ValidateCatletConfigCommand
    {
        public CatletConfig Config { get; set; }
        public Guid MachineId { get; set; }
    }
}