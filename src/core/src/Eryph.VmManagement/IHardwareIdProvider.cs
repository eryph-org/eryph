using System;

namespace Eryph.VmManagement;

public interface IHardwareIdProvider
{
    Guid HardwareId { get; }

    string HashedHardwareId { get; }
}
