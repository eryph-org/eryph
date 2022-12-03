using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Eryph.Configuration;
using Eryph.Modules.Controller.DataServices;
using Eryph.StateDb.Model;
using LanguageExt;

namespace Eryph.Runtime.Zero.Configuration.Storage
{
    internal class VirtualDiskDataServiceWithConfigServiceDecorator : IVirtualDiskDataService
    {
        private readonly IVirtualDiskDataService _decoratedService;
        private readonly IConfigWriterService<VirtualDisk> _configService;


        public VirtualDiskDataServiceWithConfigServiceDecorator(IVirtualDiskDataService decoratedService,
            IConfigWriterService<VirtualDisk> configService)
        {
            _decoratedService = decoratedService;
            _configService = configService;
        }


        public Task<Option<VirtualDisk>> GetVHD(Guid id)
        {
            return _decoratedService.GetVHD(id);
        }

        public async Task<VirtualDisk> AddNewVHD(VirtualDisk virtualDisk)
        {
            var result = await _decoratedService.AddNewVHD(virtualDisk);
            await _configService.Add(virtualDisk, virtualDisk.Project.Name);

            return result;
        }

        public Task<IEnumerable<VirtualDisk>> FindVHDByLocation(string dataStore, string project, string environment, string storageIdentifier, string name)
        {
            return _decoratedService.FindVHDByLocation(dataStore, project, environment, storageIdentifier, name);
        }

        public async Task<VirtualDisk> UpdateVhd(VirtualDisk virtualDisk)
        {
            var res = await _decoratedService.UpdateVhd(virtualDisk);
            await _configService.Update(res, virtualDisk.Project.Name);

            return res;
        }

        public async Task<Unit> DeleteVHD(Guid id)
        {
            var optionalData = await _decoratedService.GetVHD(id);
            await _decoratedService.DeleteVHD(id);
            await optionalData.IfSomeAsync(data => _configService.Delete(data,data.Project.Name ));

            return Unit.Default;
        }
    }
}