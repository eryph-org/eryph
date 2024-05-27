using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Eryph.StateDb.Model;
using LanguageExt;

namespace Eryph.Modules.Controller.DataServices;

public interface IVirtualDiskDataService
{
    Task<Option<VirtualDisk>> GetVHD(Guid id);
    Task<VirtualDisk> AddNewVHD(VirtualDisk virtualDisk);

    Task<IEnumerable<VirtualDisk>> FindVHDByLocation(
        Guid projectId, string dataStore, string environment, string storageIdentifier, 
        string name, Guid diskIdentifier);
    Task<IEnumerable<VirtualDisk>> FindOutdated(DateTimeOffset lastSeenBefore, string agentName);

    Task<VirtualDisk> UpdateVhd(VirtualDisk virtualDisk);
    Task<Unit> DeleteVHD(Guid id);
}