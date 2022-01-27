using System;

namespace Eryph.Runtime.Zero.Configuration.Storage;

internal class StorageVhdConfig
{
    public Guid Id { get; set; }

    public string Name { get; set; }
    public DateTime LastSeen { get; set; }

}