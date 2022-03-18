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

    Task<IEnumerable<VirtualDisk>> FindVHDByLocation(string dataStore, string project, string environment, string storageIdentifier, string name);

    Task<VirtualDisk> UpdateVhd(VirtualDisk virtualDisk);
    Task<Unit> DeleteVHD(Guid id);
}