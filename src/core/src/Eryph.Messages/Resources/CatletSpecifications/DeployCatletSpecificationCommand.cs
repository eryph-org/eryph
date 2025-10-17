using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Eryph.Messages.Resources.CatletSpecifications;

[SendMessageTo(MessageRecipient.Controllers)]
public class DeployCatletSpecificationCommand : ICommandWithName, IHasCorrelationId
{
    public Guid Id { get; set; }

    public string Name { get; set; }

    public Guid CorrelationId { get; set; }

    public string GetCommandName() => $"Deploy catlet specification {Name}";
}

