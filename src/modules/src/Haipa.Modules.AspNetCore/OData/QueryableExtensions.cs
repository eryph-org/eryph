using System.Linq;

namespace Haipa.Modules.AspNetCore.OData
{
    public static class QueryableExtensions
    {
        public static IQueryable<TModel> ForMappedQuery<TModel>(this IQueryable queryable, object parameters = null)
        {
            return new MappedResult<TModel>(queryable, parameters);
        }
    }
}