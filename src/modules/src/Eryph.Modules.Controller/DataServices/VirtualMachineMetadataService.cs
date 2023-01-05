using System;
using System.Text.Json;
using System.Threading.Tasks;
using Eryph.Resources.Machines;
using Eryph.StateDb;
using JetBrains.Annotations;
using LanguageExt;

namespace Eryph.Modules.Controller.DataServices
{
    internal class VirtualMachineMetadataService : IVirtualMachineMetadataService
    {
        private readonly IStateStoreRepository<StateDb.Model.VirtualMachineMetadata> _repository;

        public VirtualMachineMetadataService(IStateStoreRepository<StateDb.Model.VirtualMachineMetadata> repository)
        {
            _repository = repository;
        }

        public async Task<Option<VirtualCatletMetadata>> GetMetadata(Guid id)
        {
            if (id == Guid.Empty)
                return Option<VirtualCatletMetadata>.None;

            var entity = await _repository.GetByIdAsync(id);

            if(entity == null)
                return Option<VirtualCatletMetadata>.None;

            return DeserializeMetadataEntity(entity);
        }

        public async Task<Unit> SaveMetadata(VirtualCatletMetadata metadata)
        {
            var entity = await _repository.GetByIdAsync(metadata.Id);
            if (entity == null)
            {
                await _repository.AddAsync(new StateDb.Model.VirtualMachineMetadata
                {
                    Id = metadata.Id,
                    Metadata = JsonSerializer.Serialize(metadata)
                });

                return Unit.Default;
            }

            entity.Metadata = JsonSerializer.Serialize(metadata);
            await _repository.UpdateAsync(entity);
            return Unit.Default;
        }

        public async Task<Unit> RemoveMetadata(Guid id)
        {
            var entity = await _repository.GetByIdAsync(id);
            if(entity == null)
                return Unit.Default;

            await _repository.DeleteAsync(entity);

            return Unit.Default;
        }

        private static Option<VirtualCatletMetadata> DeserializeMetadataEntity(
            [CanBeNull] StateDb.Model.VirtualMachineMetadata metadataEntity)
        {
            return JsonSerializer.Deserialize<VirtualCatletMetadata>(metadataEntity.Metadata);
        }
    }
}