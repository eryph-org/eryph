using Eryph.ConfigModel.Catlets;
using Eryph.Core.Genetics;
using System.Collections.Generic;

namespace Eryph.Modules.Controller.Compute;

internal class ValidateCatletSpecificationSagaData
{
    public ValidateCatletSpecificationSagaState State { get; set; }

    public string? ConfigYaml { get; set; }

    public CatletConfig? BuiltConfig { get; set; }

    public IReadOnlyDictionary<UniqueGeneIdentifier, GeneHash>? ResolvedGenes { get; set; }
}
