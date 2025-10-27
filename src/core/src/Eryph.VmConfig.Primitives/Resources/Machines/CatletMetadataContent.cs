using System.Collections.Generic;
using Eryph.ConfigModel.Catlets;
using Eryph.Core.Genetics;

namespace Eryph.Resources.Machines;

// The change tracking in the controller module must be updated when modifying this class.
public sealed class CatletMetadataContent
{
    public Architecture Architecture { get; set; }

    public IReadOnlyDictionary<UniqueGeneIdentifier, GeneHash> PinnedGenes { get; set; } = new Dictionary<UniqueGeneIdentifier, GeneHash>();

    public string ContentType { get; set; }

    /// <summary>
    /// The original configuration of the catlet. It can be either
    /// YAML or JSON depending on the <see cref="ContentType"/>.
    /// </summary>
    /// <remarks>
    /// The line breaks must be normalized to LF-only.
    /// </remarks>
    public string OriginalConfig { get; set; }

    /// <summary>
    /// The <see cref="CatletConfigType.Instance"/> configuration which
    /// was used to create this catlet.
    /// </summary>
    public CatletConfig Config { get; set; }
}
