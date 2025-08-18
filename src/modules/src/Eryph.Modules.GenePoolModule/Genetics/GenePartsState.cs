using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LanguageExt;

using static LanguageExt.Prelude;

namespace Eryph.Modules.GenePool.Genetics;

internal class GenePartsState : IDisposable
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
        //
    }
}
