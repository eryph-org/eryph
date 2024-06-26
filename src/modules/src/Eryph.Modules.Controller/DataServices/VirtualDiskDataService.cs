﻿using System;
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
            Guid projectId, string dataStore, string environment, string storageIdentifier, 
            string name, Guid diskIdentifier)
        {
            return await _stateStore.For<VirtualDisk>().ListAsync(
                new VirtualDiskSpecs.GetByLocation(projectId,dataStore, 
                    environment, storageIdentifier, name, diskIdentifier));
        }

        public async Task<IEnumerable<VirtualDisk>> FindOutdated(DateTimeOffset lastSeenBefore
        , string agentName)
        {
            return await _stateStore.For<VirtualDisk>().ListAsync(
            new VirtualDiskSpecs.FindOutdated(lastSeenBefore, agentName));
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