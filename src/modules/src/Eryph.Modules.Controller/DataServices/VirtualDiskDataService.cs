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
        private readonly IStateStoreRepository<VirtualDisk> _repository;

        public VirtualDiskDataService(IStateStoreRepository<VirtualDisk> repository)
        {
            _repository = repository;
        }

        public async Task<Option<VirtualDisk>> GetVHD(Guid id)
        {
            var res = await _repository.GetByIdAsync(id);
            return res;
        }

        public async Task<VirtualDisk> AddNewVHD([NotNull] VirtualDisk virtualDisk)
        {
            if (virtualDisk.Id == Guid.Empty)
                throw new ArgumentException($"{nameof(VirtualDisk.Id)} is missing", nameof(virtualDisk));

            var res = await _repository.AddAsync(virtualDisk);
            return res;
        }

        public async Task<IEnumerable<VirtualDisk>> FindVHDByLocation(string dataStore, string project, string environment, string storageIdentifier, string name)
        {
            return await _repository.ListAsync(
                new VirtualDiskSpecs.GetByLocation(dataStore, project, environment, storageIdentifier, name));
        }

        public async Task<VirtualDisk> UpdateVhd(VirtualDisk virtualDisk)
        {
            await _repository.UpdateAsync(virtualDisk);
            return virtualDisk;
        }
    }
}