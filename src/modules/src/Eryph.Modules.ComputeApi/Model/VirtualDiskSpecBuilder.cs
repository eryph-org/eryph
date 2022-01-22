using System;
using Ardalis.Specification;
using Eryph.Modules.AspNetCore.ApiProvider;
using Eryph.Modules.AspNetCore.ApiProvider.Model;
using Eryph.StateDb.Model;
using Eryph.StateDb.Specifications;

namespace Eryph.Modules.ComputeApi.Model
{
    public class VirtualDiskSpecBuilder : ISingleResourceSpecBuilder<VirtualDisk>, IListResourceSpecBuilder<VirtualDisk>
    {
        public ISingleResultSpecification<VirtualDisk> GetSingleResourceSpec(SingleResourceRequest request)
        {
            return new ResourceSpecs<VirtualDisk>.GetById(
                Guid.Parse(request.Id), b => 
                    b.Include(x => x.AttachedDrives));
        }

        public ISpecification<VirtualDisk> GetResourceSpec(ListRequest request)
        {
            return new ResourceSpecs<VirtualDisk>.GetAll(b =>
                    b.Include(x => x.AttachedDrives));
        }
    }
}