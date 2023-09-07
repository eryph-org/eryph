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
        private readonly IStateStoreRepository<StateDb.Model.CatletMetadata> _repository;

        public VirtualMachineMetadataService(IStateStoreRepository<StateDb.Model.CatletMetadata> repository)
        {
            _repository = repository;
        }

        public async Task<Option<CatletMetadata>> GetMetadata(Guid id)
        {
            if (id == Guid.Empty)
                return Option<CatletMetadata>.None;

            var entity = await _repository.GetByIdAsync(id);

            if(entity == null)
                return Option<CatletMetadata>.None;

            return DeserializeMetadataEntity(entity);
        }

        public async Task<Unit> SaveMetadata(CatletMetadata metadata)
        {
            var entity = await _repository.GetByIdAsync(metadata.Id);
            if (entity == null)
            {
                await _repository.AddAsync(new StateDb.Model.CatletMetadata
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

        private static Option<CatletMetadata> DeserializeMetadataEntity(
            [CanBeNull] StateDb.Model.CatletMetadata metadataEntity)
        {
            return JsonSerializer.Deserialize<CatletMetadata>(metadataEntity.Metadata);
        }
    }
}