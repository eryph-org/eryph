using System;
using Eryph.ConfigModel.Catlets;

namespace Eryph.Messages.Resources.Machines.Commands
{
    [SendMessageTo(MessageRecipient.Controllers)]
    public class CreateMachineCommand : IHasCorrelationId
    {
        public CatletConfig Config { get; set; }
        public Guid CorrelationId { get; set; }
    }
}