using System;
using Eryph.ConfigModel;
using Eryph.ConfigModel.Catlets;
using Eryph.Resources;
using JetBrains.Annotations;

namespace Eryph.Messages.Resources.Catlets.Commands
{
    [SendMessageTo(MessageRecipient.Controllers)]
    public class UpdateCatletCommand : IHasCorrelationId, IHasResource
    {
        public CatletConfig Config { get; set; }

        [CanBeNull] public CatletConfig BreedConfig { get; set; }

        public Guid CorrelationId { get; set; }

        public Guid CatletId { get; set; }

        public Resource Resource => new(ResourceType.Catlet, CatletId);
    }
}