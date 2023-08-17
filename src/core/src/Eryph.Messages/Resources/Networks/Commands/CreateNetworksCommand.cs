using System;
using Eryph.ConfigModel.Catlets;
using Eryph.ConfigModel.Networks;

namespace Eryph.Messages.Resources.Networks.Commands
{
    [SendMessageTo(MessageRecipient.Controllers)]
    public class CreateNetworksCommand : IHasCorrelationId
    {
        public ProjectNetworksConfig Config { get; set; }
        public Guid CorrelationId { get; set; }
    }
}