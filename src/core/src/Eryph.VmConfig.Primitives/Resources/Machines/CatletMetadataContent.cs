using System;
using System.Collections.Generic;
using Eryph.ConfigModel;
using Eryph.ConfigModel.Catlets;
using Eryph.ConfigModel.Variables;
using Eryph.Core.Genetics;
using JetBrains.Annotations;

namespace Eryph.Resources.Machines;

// The change tracking in the controller module must be updated when modifying this class.
public sealed class CatletMetadataContent
{
    public Architecture Architecture { get; set; }

    // TODO Should we store the variables separately?
    public IReadOnlyDictionary<VariableName, string> Variables { get; set; } = new Dictionary<VariableName, string>();

    // new metadata
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
