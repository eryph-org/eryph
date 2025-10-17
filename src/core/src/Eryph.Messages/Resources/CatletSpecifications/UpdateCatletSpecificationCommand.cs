using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.Messages;


namespace Eryph.Messages.Resources.CatletSpecifications;

[SendMessageTo(MessageRecipient.Controllers)]
internal class UpdateCatletSpecificationCommand : IHasCorrelationId, IHasResource
{

}
