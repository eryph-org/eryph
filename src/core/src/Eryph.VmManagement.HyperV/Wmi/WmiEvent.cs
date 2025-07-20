using System;

namespace Eryph.VmManagement.Wmi;

/// <summary>
/// Represents a WMI event which has been raised
/// by <see cref="System.Management.EventQuery"/>.
/// </summary>
public record WmiEvent(
    DateTimeOffset Created,
    WmiObject TargetInstance);
