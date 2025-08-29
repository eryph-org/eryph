using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Eryph.Resources.Machines;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using Eryph.StateDb.Specifications;
using JetBrains.Annotations;
using LanguageExt;
using Rebus.Retry.FailFast;

namespace Eryph.Modules.Controller.DataServices
{
    internal class VirtualMachineDataService : IVirtualMachineDataService
    {
        private readonly IVirtualMachineMetadataService _metadataService;
        private readonly IStateStoreRepository<Catlet> _repository;
        private readonly IStateStore _stateStore;

        public VirtualMachineDataService(
            IStateStoreRepository<Catlet> repository,
            IVirtualMachineMetadataService metadataService,
            IStateStore stateStore)
        {
            _repository = repository;
            _metadataService = metadataService;
            _stateStore = stateStore;
        }

        public async Task<Catlet?> GetByVmId(Guid id)
        {
            return await _repository.GetBySpecAsync(new CatletSpecs.GetByVMId(id));
        }

        public async Task<Option<Catlet>> Get(Guid id)
        {
            var res = await _repository.GetBySpecAsync(new CatletSpecs.GetById(id));
            return res!;
        }

        public async Task<Catlet> Add(Catlet catlet)
        {
            if (catlet.ProjectId == Guid.Empty)
                throw new ArgumentException($"{nameof(Catlet.ProjectId)} is missing", nameof(catlet));

            if (catlet.Id == Guid.Empty)
                throw new ArgumentException($"{nameof(Catlet.Id)} is missing", nameof(catlet));

            if (catlet.MetadataId == Guid.Empty)
                throw new ArgumentException($"{nameof(Catlet.MetadataId)} is missing", nameof(catlet));

            if (catlet.VmId == Guid.Empty)
                throw new ArgumentException($"{nameof(Catlet.VmId)} is missing", nameof(catlet));

            return await _repository.AddAsync(catlet);
        }

        public async Task Remove(Guid id)
        {
            var entity = await _repository.GetByIdAsync(id);
            if (entity is null)
                return;

            // Remove floating ports
            var catletPorts = await _stateStore.For<CatletNetworkPort>().ListAsync(
                new CatletNetworkPortSpecs.GetByCatletMetadataId(entity.MetadataId));

            await _stateStore.For<FloatingNetworkPort>().DeleteRangeAsync(
                catletPorts.Where(cp => cp.FloatingPort is not null).Select(cp => cp.FloatingPort!));

            await _repository.DeleteAsync(entity);
            await _metadataService.RemoveMetadata(entity.MetadataId);
        }

        public async Task<IReadOnlyList<Guid>> GetAllVmIds(string agent)
        {
            return await _stateStore.Read<Catlet>().ListAsync(
                new CatletSpecs.GetAllVmIds(agent));
        }
    }
}