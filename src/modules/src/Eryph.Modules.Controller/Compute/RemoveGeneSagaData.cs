using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.Core.Genetics;

namespace Eryph.Modules.Controller.Compute;

public class RemoveGeneSagaData
{
    public string AgentName { get; set; }

    public GeneIdentifierWithType GeneId { get; set; }
}
