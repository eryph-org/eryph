using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Eryph.StateDb.Model;

public class CatletMetadataGene
{
    public required Guid MetadataId {get; set; }

    public required string GeneSet { get; set; }

    public required string Name { get; set; }

    public required string Architecture { get; set; }

    /// <summary>
    /// This property is only used optimize database queries
    /// and should not be used directly outside of queries.
    /// </summary>
    internal string UniqueGeneIndex
    {
        get => StateStoreGeneExtensions.ToUniqueGeneIndex(GeneSet, Name, Architecture)!;
        // The setter is only defined so EF Core persists the property to the
        // database (for indexing). It does not update the property.
#pragma warning disable S1144
        private set => _ = value;
#pragma warning restore S1144
    }
}
