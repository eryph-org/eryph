using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Eryph.Core;

public static class GuidExtensions
{
    public static Guid GetOrGenerate(this Guid guid) =>
        guid == Guid.Empty ? Guid.NewGuid() : guid;

    public static Guid GetOrGenerate(this Guid? guid) =>
        GetOrGenerate(guid.GetValueOrDefault());
}
