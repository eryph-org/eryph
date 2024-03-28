using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Eryph.ZeroState;

internal interface IZeroStateChangeHandler<TChange>
{
    Task HandleChangeAsync(
        TChange change,
        CancellationToken cancellationToken = default);
}