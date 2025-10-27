using System;
using System.Collections.Generic;
using Eryph.ConfigModel.Catlets;
using Eryph.ConfigModel.Json;
using Eryph.Core.Genetics;
using Eryph.StateDb;
using Eryph.StateDb.Model;

namespace Eryph.Modules.Controller.Compute;

public class CatletSpecificationVersionVariantSagaData
{
    public required Architecture Architecture { get; set; }

    public required CatletConfig BuiltConfig { get; set; }

    public IReadOnlyDictionary<UniqueGeneIdentifier, GeneHash> ResolvedGenes { get; set; } = new Dictionary<UniqueGeneIdentifier, GeneHash>();

    public CatletSpecificationVersionVariant ToDbVariant(Guid specificationVersionId)
    {
        var id = Guid.NewGuid();
        return new CatletSpecificationVersionVariant
        {
            Id = id,
            SpecificationVersionId = specificationVersionId,
            Architecture = Architecture,
            BuiltConfig = CatletConfigJsonSerializer.Serialize(BuiltConfig),
            PinnedGenes = ResolvedGenes.ToGenesList(id),
        };
    }
}
