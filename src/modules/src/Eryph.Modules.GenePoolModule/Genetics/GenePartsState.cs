using System;
using System.Collections.Generic;
using Eryph.Core.Genetics;
using LanguageExt;

using static LanguageExt.Prelude;

namespace Eryph.Modules.GenePool.Genetics;

/// <summary>
/// Maintains a list of the successfully downloaded gene parts.
/// </summary>
/// <remarks>
/// This class provides an in-memory side effect which simplifies
/// the implementation of the gene pool logic.
/// </remarks>
internal sealed class GenePartsState : IDisposable
{
    private readonly Dictionary<GenePartHash, long> _existingParts = new();

    public Aff<Unit> AddPart(GenePartHash partHash, long size)
    {
        _existingParts[partHash] = size;
        return SuccessAff(unit);
    }

    public Aff<HashMap<GenePartHash, long>> GetExistingParts()
    {
        return SuccessAff(_existingParts.ToHashMap());
    }

    public void Dispose()
    {
        // The IDisposable interface is only implemented to provide
        // support for the use() function.
    }
}
