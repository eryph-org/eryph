using System;
using System.Linq;
using Ardalis.Specification;
using Eryph.StateDb.Model;

namespace Eryph.StateDb.Specifications
{
    public static class OperationSpecs
    {
        internal static void ExpandFields(ISpecificationBuilder<Operation> query, string expand, DateTimeOffset requestLogTimestamp)
        {
            if (string.IsNullOrWhiteSpace(expand)) return;


            var expandedFields = expand.Split(',', StringSplitOptions.RemoveEmptyEntries);

            foreach (var expandedField in expandedFields)
            {
                switch (expandedField)
                {
                    case "logs":
                        query.Include(x => x.LogEntries
                            .Where(l => l.Timestamp > requestLogTimestamp));
                        break;
                    case "tasks":
                        query.Include(x => x.Tasks);
                        break;
                    case "resources":
                        query.Include(x => x.Resources);
                        break;
                    case "projects":
                        query.Include(x => x.Projects).ThenInclude(x=>x.Project);
                        break;
                }
            }
        }


        public sealed class GetAll : Specification<Operation>
        {
            public GetAll(string expanded, DateTimeOffset requestLogTimestamp)
            {
                Query.OrderBy(x => x.Id);
                ExpandFields(Query, expanded, requestLogTimestamp);

            }
        }

        public sealed class GetById : Specification<Operation>, ISingleResultSpecification<Operation>
        {
            public GetById(Guid id, string expanded, DateTimeOffset requestLogTimestamp)
            {
                Query.Where(x => x.Id == id);
                ExpandFields(Query, expanded, requestLogTimestamp);


            }
        }

    }
}