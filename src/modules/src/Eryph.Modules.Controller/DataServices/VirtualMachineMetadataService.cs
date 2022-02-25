using System;
using System.Threading.Tasks;
using Eryph.Resources.Machines;
using Eryph.StateDb;
using JetBrains.Annotations;
using LanguageExt;
using Newtonsoft.Json;

namespace Eryph.Modules.Controller.DataServices
{
    internal class VirtualMachineMetadataService : IVirtualMachineMetadataService
    {
        private readonly IStateStoreRepository<StateDb.Model.VirtualMachineMetadata> _repository;

        public VirtualMachineMetadataService(IStateStoreRepository<StateDb.Model.VirtualMachineMetadata> repository)
        {
            _repository = repository;
        }

        public async Task<Option<VirtualMachineMetadata>> GetMetadata(Guid id)
        {
            if (id == Guid.Empty)
                return Option<VirtualMachineMetadata>.None;

            var entity = await _repository.GetByIdAsync(id);

            if(entity == null)
                return Option<VirtualMachineMetadata>.None;

            return DeserializeMetadataEntity(entity);
        }

        public async Task<Unit> SaveMetadata(VirtualMachineMetadata metadata)
        {
            var entity = await _repository.GetByIdAsync(metadata.Id);
            if (entity == null)
            {
                await _repository.AddAsync(new StateDb.Model.VirtualMachineMetadata
                {
                    Id = metadata.Id,
                    Metadata = JsonConvert.SerializeObject(metadata)
                });

                return Unit.Default;
            }

            entity.Metadata = JsonConvert.SerializeObject(metadata);
            await _repository.UpdateAsync(entity);
            return Unit.Default;
        }

        private static Option<VirtualMachineMetadata> DeserializeMetadataEntity(
            [CanBeNull] StateDb.Model.VirtualMachineMetadata metadataEntity)
        {
            return JsonConvert.DeserializeObject<VirtualMachineMetadata>(metadataEntity.Metadata);
        }
    }
}