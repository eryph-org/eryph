using System;
using Eryph.Messages.Resources.Catlets.Events;

namespace Eryph.Messages.Resources.Networks.Commands;

public class UpdateProjectNetworkPlanResponse
{
    public Guid ProjectId { get; set; }

    public NetworkNeighborRecord[] UpdatedAddresses { get; set; }
}
