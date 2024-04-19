using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LanguageExt;
using Microsoft.EntityFrameworkCore;

namespace Eryph.Modules.Controller.ChangeTracking
{
    internal interface IChangeDetector<TChange>
    {
        Task<Seq<TChange>> DetectChanges(
            DbContext dbContext,
            CancellationToken cancellationToken = default);
    }
}
