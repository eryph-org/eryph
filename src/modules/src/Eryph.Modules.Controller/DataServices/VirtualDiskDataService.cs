using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using Eryph.StateDb.Specifications;
using JetBrains.Annotations;
using LanguageExt;

namespace Eryph.Modules.Controller.DataServices
{
    internal class VirtualDiskDataService : IVirtualDiskDataService
    {
        private readonly IStateStore _stateStore;

        public VirtualDiskDataService(IStateStore stateStore)
        {
            _stateStore = stateStore;
        }

        public async Task<Option<VirtualDisk>> GetVHD(Guid id)
        {
            var res = await _stateStore.For<VirtualDisk>().GetByIdAsync(id);
            return res;
        }

        public async Task<VirtualDisk> AddNewVHD([NotNull] VirtualDisk virtualDisk)
        {
            if (virtualDisk.Id == Guid.Empty)
                throw new ArgumentException($"{nameof(VirtualDisk.Id)} is missing", nameof(virtualDisk));


            var res = await _stateStore.For<VirtualDisk>().AddAsync(virtualDisk);
            return res;
        }

        public async Task<IEnumerable<VirtualDisk>> FindVHDByLocation(
            string dataStore, string projectName, string environment, string storageIdentifier, string name)
        {
            return await _stateStore.For<VirtualDisk>().ListAsync(
                new VirtualDiskSpecs.GetByLocation(dataStore, projectName, environment, storageIdentifier, name));
        }

        public async Task<IEnumerable<VirtualDisk>> FindOutdated(DateTimeOffset lastSeenBefore)
        {
            return await _stateStore.For<VirtualDisk>().ListAsync(
            new VirtualDiskSpecs.FindOutdated(lastSeenBefore));
        }
        public async Task<VirtualDisk> UpdateVhd(VirtualDisk virtualDisk)
        {
            await _stateStore.For<VirtualDisk>().UpdateAsync(virtualDisk);
            return virtualDisk;
        }

        public async Task<Unit> DeleteVHD(Guid id)
        {
            var res = await _stateStore.For<VirtualDisk>().GetByIdAsync(id);

            if(res!= null)
                await _stateStore.For<VirtualDisk>().DeleteAsync(res);

            return Unit.Default;
        }
    }
}