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

    // The single architecture whose built config/genes are returned in the response. Chosen
    // deterministically up front (the default when requested, otherwise the first by ordinal) so
    // the result never depends on which architecture's build happens to finish last.
    public Architecture? PrimaryArchitecture { get; set; }

    public CatletConfig? BuiltConfig { get; set; }

    public IReadOnlyDictionary<UniqueGeneIdentifier, GeneHash>? ResolvedGenes { get; set; }
}
