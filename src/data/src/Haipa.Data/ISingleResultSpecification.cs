using System;
using System.Collections.Generic;
using System.Text;
using Ardalis.Specification;

namespace Haipa.Data
{
    public interface ISingleResultSpecification<TModel> : ISpecification<TModel>, ISingleResultSpecification
        where TModel : class
    {

    }
}
