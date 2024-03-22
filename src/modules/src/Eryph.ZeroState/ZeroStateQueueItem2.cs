using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Eryph.ZeroState
{
    public class ZeroStateQueueItem2<TChange>
    {
        public Guid TransactionId { get; set; }

        public TChange Changes { get; init; }
    }
}
