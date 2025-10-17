using Eryph.ConfigModel.Catlets;
using Eryph.Core.Genetics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Eryph.Modules.Controller.Compute;

public class UpdateCatletSpecificationSagaData
{
    public string? ConfigYaml { get; set; }

    public string? Comment { get; set; }

    public CatletConfig? BuiltConfig { get; set; }

    public string? Name { get; set; }

    public string? AgentName { get; set; }

    public UpdateCatletSpecificationSagaState State { get; set; }

    public Guid SpecificationId { get; set; }

    public IReadOnlyDictionary<UniqueGeneIdentifier, GeneHash> ResolvedGenes { get; set; } = new Dictionary<UniqueGeneIdentifier, GeneHash>();
}
