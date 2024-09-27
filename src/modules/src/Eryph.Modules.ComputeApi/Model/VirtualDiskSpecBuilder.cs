using Ardalis.Specification;
using Eryph.Modules.AspNetCore;
using Eryph.StateDb.Model;

namespace Eryph.Modules.ComputeApi.Model;

public class VirtualDiskSpecBuilder(
    IUserRightsProvider userRightsProvider)
    : ResourceSpecBuilder<VirtualDisk>(userRightsProvider)
{
    protected override void CustomizeQuery(ISpecificationBuilder<VirtualDisk> query)
    {
        query.Include(x => x.AttachedDrives);
        query.Include(x => x.Children);
    }
}
