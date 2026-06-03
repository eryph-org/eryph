using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Eryph.GuestServices.HvDataExchange.Host;

namespace Eryph.Modules.HostAgent;

public interface IGuestDataWriter
{
    Task SetExternalAsync(Guid vmId, IReadOnlyDictionary<string, string?> values);
}

/// <summary>
/// Applies values to the guest's External KVP pool (host → guest). A null value
/// removes the key. Single write path for every guest-services setting (shell,
/// authorized keys, ...).
/// </summary>
public sealed class GuestDataWriter(IHostDataExchange hostDataExchange) : IGuestDataWriter
{
    public Task SetExternalAsync(Guid vmId, IReadOnlyDictionary<string, string?> values) =>
        hostDataExchange.SetExternalValuesAsync(vmId, values);
}
