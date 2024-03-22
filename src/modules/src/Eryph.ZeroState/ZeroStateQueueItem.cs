using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Eryph.ZeroState
{
    public class ZeroStateQueueItem
    {
        public Guid TransactionId { get; set; }

        public List<ZeroStateChange> Changes { get; set; } = new List<ZeroStateChange>();
    }

    public class ZeroStateChange
    {
        public Guid Id { get; set; }

        public Type EntityType { get; set; }
    }
}
