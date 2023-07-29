using System;
using Eryph.ConfigModel.Catlets;
using Eryph.Resources;
using LanguageExt.Pipes;

namespace Eryph.Messages.Resources.Catlets.Commands
{
    [SendMessageTo(MessageRecipient.Controllers)]
    public class ValidateCatletConfigCommand : IHasResource
    {
        public CatletConfig Config { get; set; }
        public Guid MachineId { get; set; }
        public Resource Resource => new(ResourceType.Catlet, MachineId);

    }
}