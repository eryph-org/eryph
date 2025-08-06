using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.ConfigModel.Catlets;
using Eryph.Core.Genetics;

namespace Eryph.Modules.Controller.Compute;

internal class ResolveCatletSpecificationSagaData
{
    public ResolveCatletSpecificationSagaState State { get; set; }

    public string ConfigYaml { get; set; }

    public Architecture Architecture { get; set; }

    public CatletConfig? Config { get; set; }

    public CatletConfig? ExpandedConfig { get; set; }

    public CatletConfig? ParentConfig { get; set; }

    // The agent which is hosting the gene pool
    public string? AgentName { get; set; }
}
