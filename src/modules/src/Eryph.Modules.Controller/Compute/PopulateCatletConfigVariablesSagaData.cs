using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.ConfigModel.Catlets;
using Eryph.Core.Genetics;

namespace Eryph.Modules.Controller.Compute;

internal class PopulateCatletConfigVariablesSagaData
{
    public CatletConfig? Config { get; set; }

    public string? AgentName { get; set; }

    public Architecture? Architecture { get; set; }
}
