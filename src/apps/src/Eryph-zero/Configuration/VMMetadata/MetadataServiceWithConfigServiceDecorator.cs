using System;
using System.Threading.Tasks;
using Eryph.Configuration;
using Eryph.Modules.Controller.DataServices;
using Eryph.Resources.Machines;
using LanguageExt;

namespace Eryph.Runtime.Zero.Configuration.VMMetadata
{
    internal class MetadataServiceWithConfigServiceDecorator : IVirtualMachineMetadataService
    {
        private readonly IConfigWriterService<CatletMetadata> _configService;
        private readonly IVirtualMachineMetadataService _decoratedService;

        public MetadataServiceWithConfigServiceDecorator(IVirtualMachineMetadataService decoratedService,
            IConfigWriterService<CatletMetadata> configService)
        {
            _decoratedService = decoratedService;
            _configService = configService;
        }


        public Task<Option<CatletMetadata>> GetMetadata(Guid id)
        {
            return _decoratedService.GetMetadata(id);
        }

        public async Task<Unit> SaveMetadata(CatletMetadata metadata)
        {
            await _decoratedService.SaveMetadata(metadata);
            await _configService.Update(metadata, "");
            return Unit.Default;
        }

        public async Task<Unit> RemoveMetadata(Guid id)
        {
            await _decoratedService.RemoveMetadata(id);
            await _configService.Delete(new CatletMetadata { Id = id }, "");
            return Unit.Default;
        }
    }
}