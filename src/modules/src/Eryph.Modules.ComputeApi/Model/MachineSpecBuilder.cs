using System;
using Ardalis.Specification;
using Eryph.Modules.AspNetCore.ApiProvider;
using Eryph.Modules.AspNetCore.ApiProvider.Model;
using Eryph.StateDb.Model;
using Eryph.StateDb.Specifications;

namespace Eryph.Modules.ComputeApi.Model
{
    public class MachineSpecBuilder : ISingleResourceSpecBuilder<Catlet>, IListResourceSpecBuilder<Catlet>
    {
        public ISingleResultSpecification<Catlet> GetSingleResourceSpec(SingleResourceRequest request)
        {
            return new ResourceSpecs<Catlet>.GetById(Guid.Parse(request.Id));
        }

        public ISpecification<Catlet> GetResourceSpec(ListRequest request)
        {
            return new ResourceSpecs<Catlet>.GetAll();

        }
    }
}