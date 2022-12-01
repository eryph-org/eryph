using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using Eryph.StateDb.Specifications;
using JetBrains.Annotations;
using LanguageExt;
using VirtualMachineMetadata = Eryph.Resources.Machines.VirtualMachineMetadata;

namespace Eryph.Modules.Controller.DataServices
{
    internal class VirtualMachineDataService : IVirtualMachineDataService
    {
        private readonly IVirtualMachineMetadataService _metadataService;
        private readonly IStateStoreRepository<VirtualCatlet> _repository;

        public VirtualMachineDataService(IStateStoreRepository<VirtualCatlet> repository,
            IVirtualMachineMetadataService metadataService)
        {
            _repository = repository;
            _metadataService = metadataService;
        }

        public async Task<Option<VirtualCatlet>> GetByVMId(Guid id)
        {
            var res = await _repository.GetBySpecAsync(new VirtualMachineSpecs.GetByVMId(id));
            return res!;
        }

        public async Task<Option<VirtualCatlet>> GetVM(Guid id)
        {
            var res = await _repository.GetByIdAsync(id);
            return res!;
        }

        public async Task<VirtualCatlet> AddNewVM(VirtualCatlet vm,
            [NotNull] VirtualMachineMetadata metadata)
        {
            if (vm.ProjectId == Guid.Empty)
                throw new ArgumentException($"{nameof(VirtualCatlet.ProjectId)} is missing", nameof(vm));

            if (vm.Id == Guid.Empty)
                throw new ArgumentException($"{nameof(VirtualCatlet.Id)} is missing", nameof(vm));

            if (vm.VMId == null)
                throw new ArgumentException($"{nameof(VirtualCatlet.VMId)} is missing", nameof(vm));

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
            
            await _repository.DeleteAsync(entity);
            

            if (entity.MetadataId != Guid.Empty)
            {
                await _metadataService.RemoveMetadata(entity.MetadataId);
            }

            return Unit.Default;
        }

        public async Task<IEnumerable<VirtualCatlet>> GetAll()
        {
            return await _repository.ListAsync();
        }
    }
}