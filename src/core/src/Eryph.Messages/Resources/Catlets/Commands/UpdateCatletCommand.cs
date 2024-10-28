using System;
using System.Collections.Generic;
using Eryph.ConfigModel.Catlets;
using Eryph.Core.Genetics;
using Eryph.Resources;
using JetBrains.Annotations;

namespace Eryph.Messages.Resources.Catlets.Commands
{
    [SendMessageTo(MessageRecipient.Controllers)]
    public class UpdateCatletCommand : IHasCorrelationId, IHasResource
    {
        public CatletConfig Config { get; set; }

        [CanBeNull] public CatletConfig BredConfig { get; set; }

        [CanBeNull] public IReadOnlyList<UniqueGeneIdentifier> ResolvedGenes { get; set; }

        public Guid CorrelationId { get; set; }

        public Guid CatletId { get; set; }

        public Resource Resource => new(ResourceType.Catlet, CatletId);
    }
}