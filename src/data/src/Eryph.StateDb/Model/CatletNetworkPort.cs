using System;
using JetBrains.Annotations;

namespace Eryph.StateDb.Model;

public class CatletNetworkPort : VirtualNetworkPort
{
    public Guid CatletMetadataId { get; set; }
}