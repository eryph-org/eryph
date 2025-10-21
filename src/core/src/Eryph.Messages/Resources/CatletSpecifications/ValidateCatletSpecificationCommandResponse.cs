using System.Collections.Generic;
using Eryph.ConfigModel.Catlets;
using Eryph.Core.Genetics;

namespace Eryph.Messages.Resources.CatletSpecifications;

[SendMessageTo(MessageRecipient.Controllers)]
public class ValidateCatletSpecificationCommandResponse
{
    public bool IsValid { get; set; }

    public CatletConfig BuiltConfig { get; set; }

    public IReadOnlyDictionary<UniqueGeneIdentifier, GeneHash> ResolvedGenes { get; set; }
}
