using System;
using Eryph.ConfigModel;

namespace Eryph.VmManagement.Data.Core;

/// <summary>
/// Represents a Hyper-V VHD disk as returned by the Cmdlet
/// <c>Get-VHD</c>.
/// </summary>
public class VhdInfo
{
    [PrivateIdentifier] public string Path { get; set; }
        
    public long Size { get; set; }

    public long? MinimumSize { get; set; }

    public long FileSize { get; set; }

    [PrivateIdentifier] public string ParentPath { get; set; }

    public Guid DiskIdentifier { get; set; }
}
