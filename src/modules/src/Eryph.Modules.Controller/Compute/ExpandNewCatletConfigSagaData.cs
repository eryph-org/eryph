using Eryph.ConfigModel;
using Eryph.ConfigModel.Catlets;
using Eryph.Core.Genetics;

namespace Eryph.Modules.Controller.Compute;

internal class ExpandNewCatletConfigSagaData
{
    public Architecture? Architecture { get; set; }
    
    public CatletConfig? Config { get; set; }

    public ProjectName? ProjectName { get; set; }

    public bool ShowSecrets { get; set; }

    public string? AgentName { get; set; }

    public ExpandNewCatletConfigSagaState State { get; set; }
}
