using Eryph.Modules.AspNetCore;

namespace Eryph.Modules.ComputeApi.Model;

public class VirtualNetworkSpecBuilder(
    IUserRightsProvider userRightsProvider)
    : ResourceSpecBuilder<StateDb.Model.VirtualNetwork>(userRightsProvider)
{
}
