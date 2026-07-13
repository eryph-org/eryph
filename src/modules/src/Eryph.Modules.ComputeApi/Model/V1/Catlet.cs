using System.Collections.Generic;
using Eryph.Modules.AspNetCore.ApiProvider.Model.V1;
using CatletStatus = Eryph.StateDb.Model.CatletStatus;
using CatletProvisioningStatus = Eryph.StateDb.Model.CatletProvisioningStatus;

namespace Eryph.Modules.ComputeApi.Model.V1;

public class Catlet
{
    public required string Id { get; set; }

    public required string Name { get; set; }

    /// <summary>
    /// The ID of the corresponding Hyper-V virtual machine.
    /// </summary>
    public required string VmId { get; set; }

    public required Project Project { get; set; }

    public required CatletStatus Status { get; set; }

    /// <summary>
    /// The provisioning status of the catlet as reported by guest-services.
    /// Tracked automatically; <c>Unknown</c> until guest-services reports a state.
    /// </summary>
    public required CatletProvisioningStatus ProvisioningStatus { get; set; }

    /// <summary>
    /// Indicates that the catlet has been created with an old
    /// version of eryph and is missing some metadata. Hence,
    /// it cannot be edited and its configuration cannot be inspected.
    /// </summary>
    public required bool IsDeprecated { get; set; }

    public IReadOnlyList<CatletNetwork>? Networks { get; set; }

    public IReadOnlyList<CatletNetworkAdapter>? NetworkAdapters { get; set; }

    public IReadOnlyList<CatletDrive>? Drives { get; set; }

    public CatletSpecificationInfo? Specification { get; set; }
}
