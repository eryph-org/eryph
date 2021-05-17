using System;
using System.Threading.Tasks;
using Haipa.Configuration;
using Haipa.Modules.Controller;
using Haipa.Primitives;
using Haipa.Primitives.Resources.Machines;
using LanguageExt;

namespace Haipa.Runtime.Zero.Configuration.VMMetadata
{
    internal class MetadataServiceWithConfigServiceDecorator : IVirtualMachineMetadataService
    {
        private readonly IVirtualMachineMetadataService _decoratedService;
        private readonly IConfigWriterService<VirtualMachineMetadata> _configService;

        public MetadataServiceWithConfigServiceDecorator(IVirtualMachineMetadataService decoratedService, IConfigWriterService<VirtualMachineMetadata> configService)
        {
            _decoratedService = decoratedService;
            _configService = configService;
        }


        public Task<Option<VirtualMachineMetadata>> GetMetadata(Guid id)
        {
            return _decoratedService.GetMetadata(id);
        }

        public async Task<Unit> SaveMetadata(VirtualMachineMetadata metadata)
        {
            await _decoratedService.SaveMetadata(metadata);
            await _configService.Update(metadata);
            return Unit.Default;
        }
    }
}