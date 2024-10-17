using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.Core.Genetics;

namespace Eryph.Modules.VmHostAgent.Inventory;

public interface IHostArchitectureProvider
{
    public GeneArchitecture Architecture { get; }
}