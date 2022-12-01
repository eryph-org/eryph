using System;
using System.Numerics;

namespace Eryph.StateDb.Model;


public class IpAssignment
{
    public Guid Id { get; set; }
    public Guid SubnetId { get; set; }
    public Subnet Subnet { get; set; }


    public string IpAddress { get; set; }

    public Guid? NetworkPortId { get; set; }
    public VirtualNetworkPort NetworkPort { get; set; }


}