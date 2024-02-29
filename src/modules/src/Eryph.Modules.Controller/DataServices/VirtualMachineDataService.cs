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
using CatletMetadata = Eryph.Resources.Machines.CatletMetadata;

namespace Eryph.Modules.Controller.DataServices
{
    internal class VirtualMachineDataService : IVirtualMachineDataService
    {
        private readonly IVirtualMachineMetadataService _metadataService;
        private readonly IStateStoreRepository<Catlet> _repository;
        private readonly IStateStore _stateStore;

        public VirtualMachineDataService(IStateStoreRepository<Catlet> repository,
            IVirtualMachineMetadataService metadataService, IStateStore stateStore)
        {
            _repository = repository;
            _metadataService = metadataService;
            _stateStore = stateStore;
        }

        public async Task<Option<Catlet>> GetByVMId(Guid id)
        {
            var res = await _repository.GetBySpecAsync(new CatletSpecs.GetByVMId(id));
            return res!;
        }

        public async Task<Option<Catlet>> GetVM(Guid id)
        {
            var res = await _repository.GetBySpecAsync(new CatletSpecs.GetById(id));
            return res!;
        }

        public async Task<Catlet> AddNewVM(Catlet vm,
            [NotNull] CatletMetadata metadata)
        {
            if (vm.ProjectId == Guid.Empty)
                throw new ArgumentException($"{nameof(Catlet.ProjectId)} is missing", nameof(vm));

            if (vm.Id == Guid.Empty)
                throw new ArgumentException($"{nameof(Catlet.Id)} is missing", nameof(vm));

            if (vm.VMId == null)
                throw new ArgumentException($"{nameof(Catlet.VMId)} is missing", nameof(vm));

            if (metadata.Id == null)
                throw new ArgumentException($"{nameof(metadata.Id)} is missing", nameof(metadata));

            if (metadata.VMId != vm.VMId)
                throw new ArgumentException($"{nameof(metadata.VMId)} is invalid.", nameof(metadata));

            if (metadata.MachineId != vm.Id)
                throw new ArgumentException($"{nameof(metadata.MachineId)} is invalid.", nameof(metadata));


            await _metadataService.SaveMetadata(metadata);

            vm.MetadataId = metadata.Id;

            var res = await _repository.AddAsync(vm);
            return res;
        }

        public async Task<Unit> RemoveVM(Guid id)
        {
            var entity = await _repository.GetByIdAsync(id);
            if (entity == null)
                return Unit.Default;

            // remove floating port references
            await _stateStore.LoadCollectionAsync(entity, x=>x.NetworkPorts);
            var floatingPortRepository = _stateStore.For<FloatingNetworkPort>();
            foreach (var entityNetworkPort in entity.NetworkPorts)
            {
                await _stateStore.LoadPropertyAsync(entityNetworkPort, x => x.FloatingPort);
            }
            
            await floatingPortRepository.DeleteRangeAsync(entity.NetworkPorts
                .Where(x => x.FloatingPort != null).Select(x => x.FloatingPort));


            await _repository.DeleteAsync(entity);
            

            if (entity.MetadataId != Guid.Empty)
            {
                await _metadataService.RemoveMetadata(entity.MetadataId);
            }

            return Unit.Default;
        }

        public async Task<IEnumerable<Catlet>> GetAll()
        {
            return await _repository.ListAsync();
        }
    }
}