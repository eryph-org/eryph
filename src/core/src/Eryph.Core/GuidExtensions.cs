using System;

namespace Eryph.Core;

public static class GuidExtensions
{
    public static Guid GetOrGenerate(this Guid guid) =>
        guid == Guid.Empty ? Guid.NewGuid() : guid;

    public static Guid GetOrGenerate(this Guid? guid) => guid.GetValueOrDefault().GetOrGenerate();
}
