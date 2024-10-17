using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.ConfigModel;
using Eryph.Core.Genetics;

namespace Eryph.Messages.Resources.Catlets.Commands;

public class ResolveGenesCommandResponse
{
    public IReadOnlyDictionary<GeneIdentifier, GeneArchitecture> ResolvedArchitectures { get; set; }
}
