using System;
using Eryph.Resources;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Eryph.Messages.Resources.CatletSpecifications;

internal class DeleteCatletSpecificationCommand : IHasCorrelationId, IHasResource
{
    public Guid CorrelationId { get; set; }

    public Resource Resource => throw new NotImplementedException();
}
