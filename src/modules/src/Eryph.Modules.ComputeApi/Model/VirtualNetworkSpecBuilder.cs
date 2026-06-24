using Eryph.Modules.AspNetCore;
using Eryph.StateDb.Model;

namespace Eryph.Modules.ComputeApi.Model;

public class VirtualNetworkSpecBuilder(
    IUserRightsProvider userRightsProvider)
    : ResourceSpecBuilder<VirtualNetwork>(userRightsProvider);
