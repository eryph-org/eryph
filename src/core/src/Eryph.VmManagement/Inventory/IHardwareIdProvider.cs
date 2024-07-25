using System;

namespace Eryph.VmManagement.Inventory;

public interface IHardwareIdProvider
{
    Guid HardwareId { get; }

    string HashedHardwareId { get; }
}
