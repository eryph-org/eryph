using Ardalis.Specification;
using Eryph.Modules.AspNetCore;
using Eryph.StateDb.Model;

namespace Eryph.Modules.ComputeApi.Model
{
    public class VirtualDiskSpecBuilder : ResourceSpecBuilder<VirtualDisk>
    {
        public VirtualDiskSpecBuilder(IUserRightsProvider userRightsProvider) : base(userRightsProvider)
        {
        }


        protected override void CustomizeQuery(ISpecificationBuilder<VirtualDisk> specification)
        {
            specification.Include(x => x.AttachedDrives);
        }

    }
}