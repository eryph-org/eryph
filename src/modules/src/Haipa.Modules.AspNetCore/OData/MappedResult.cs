using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace Haipa.Modules.AspNetCore.OData
{
    public class MappedResult<TModel> : IQueryable<TModel>, IMappedResult
    {
        public IQueryable EntityQueryable { get; }
        public Type ModelType => typeof(TModel);
        public object Parameters { get; set; }


        public MappedResult(IQueryable innerQueryable, object parameters = null)
        {
            Parameters = parameters;
            EntityQueryable = innerQueryable;
        }

        public IEnumerator<TModel> GetEnumerator()
        {
            throw new NotSupportedException();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            throw new NotSupportedException();
        }

        public Type ElementType => throw new NotSupportedException();

        public Expression Expression => throw new NotSupportedException();
        public IQueryProvider Provider => throw new NotSupportedException();
    }
}