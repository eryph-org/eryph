using System;
using Ardalis.Specification;
using Haipa.Data;
using Haipa.Modules.AspNetCore.ApiProvider;
using Haipa.Modules.AspNetCore.ApiProvider.Model;
using Haipa.StateDb.Model;
using Haipa.StateDb.Specifications;

namespace Haipa.Modules.ComputeApi.Model
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