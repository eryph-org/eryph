using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.ConfigModel.Catlets;
using Eryph.Core;

namespace Eryph.Messages.Resources.Catlets.Commands;

[SendMessageTo(MessageRecipient.Controllers)]
public class BreedCatletCommand : IHasCorrelationId, ICommandWithName
{
    public string AgentName { get; set; }

    public CatletConfig Config { get; set; }

    public Guid CorrelationId { get; set; }

    public string GetCommandName() => $"Breed catlet {Config?.Name ?? "Catlet"}";
}
