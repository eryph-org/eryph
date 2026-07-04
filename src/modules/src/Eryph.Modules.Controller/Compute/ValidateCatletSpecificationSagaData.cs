using System.Collections.Generic;
using Eryph.ConfigModel.Catlets;
using Eryph.Core.Genetics;

namespace Eryph.Modules.Controller.Compute;

internal class ValidateCatletSpecificationSagaData
{
    public ValidateCatletSpecificationSagaState State { get; set; }

    public string? ConfigYaml { get; set; }

    // The architectures still being built. Validation succeeds once every requested architecture
    // has built; a build failure fails the whole validation.
    public ISet<Architecture> PendingArchitectures { get; set; } = new HashSet<Architecture>();

    public CatletConfig? BuiltConfig { get; set; }

    public IReadOnlyDictionary<UniqueGeneIdentifier, GeneHash>? ResolvedGenes { get; set; }
}
