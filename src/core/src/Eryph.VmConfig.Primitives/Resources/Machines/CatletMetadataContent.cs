using System.Collections.Generic;
using Eryph.ConfigModel.Catlets;
using Eryph.Core.Genetics;

namespace Eryph.Resources.Machines;

// The change tracking in the controller module must be updated when modifying this class.
public sealed class CatletMetadataContent
{
    public Architecture Architecture { get; set; }

    public IReadOnlyDictionary<UniqueGeneIdentifier, GeneHash> PinnedGenes { get; set; } = new Dictionary<UniqueGeneIdentifier, GeneHash>();

    /// <summary>
    /// The original YAML configuration of the catlet.
    /// </summary>
    /// <remarks>
    /// The line breaks must be normalized to LF-only.
    /// </remarks>
    public string ConfigYaml { get; set; }

    public CatletConfig BuiltConfig { get; set; }
}
