using System;
using System.Linq;
using Ardalis.Specification;
using Eryph.StateDb.Model;

namespace Eryph.StateDb.Specifications
{
    public static class OperationSpecs
    {

        public sealed class GetAll : Specification<Operation>
        {
            public GetAll(bool expanded, DateTimeOffset requestLogTimestamp)
            {
                Query.OrderBy(x => x.Id);

                if (!expanded) return;

                Query
                    .Include(x => x.Resources)
                    .Include(x => x.LogEntries.Where(l => l.Timestamp > requestLogTimestamp))
                    .Include(x => x.Tasks);
            }
        }

        public sealed class GetById : Specification<Operation>, ISingleResultSpecification<Operation>
        {
            public GetById(Guid id, bool expanded, DateTimeOffset requestLogTimestamp)
            {
                Query.Where(x => x.Id == id);

                if (!expanded) return;

                Query.Include(x => x.Resources)
                    .Include(x => x.LogEntries.Where(l => l.Timestamp > requestLogTimestamp))
                    .Include(x => x.Tasks);

            }
        }

    }
}