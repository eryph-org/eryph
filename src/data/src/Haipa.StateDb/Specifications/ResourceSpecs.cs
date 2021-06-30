using System;
using Ardalis.Specification;
using Haipa.Data;
using Haipa.StateDb.Model;

namespace Haipa.StateDb.Specifications
{
    public static class ResourceSpecs<T> where T: Resource
    {

        public sealed class GetAll : Specification<T>
        {
            public GetAll(Action<ISpecificationBuilder<T>> customizeAction = null)
            {
                Query.OrderBy(x => x.Id);
                customizeAction?.Invoke(Query);

            }
        }

        public sealed class GetByName : Specification<T>
        {
            public GetByName(string name, Action<ISpecificationBuilder<T>> customizeAction = null)
            {
                Query.Where(x => x.Name == name);
                customizeAction?.Invoke(Query);
            }
        }

        public sealed class GetById : Specification<T>, ISingleResultSpecification<T>
        {
            public GetById(Guid id, Action<ISpecificationBuilder<T>> customizeAction = null)
            {
                Query.Where(x => x.Id == id);
                customizeAction?.Invoke(Query);

            }
        }
    }




}