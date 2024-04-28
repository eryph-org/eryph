using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Eryph.Configuration;
using Eryph.Modules.Controller.DataServices;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using LanguageExt;

namespace Eryph.Runtime.Zero.Configuration.Storage
{
    internal class VirtualDiskDataServiceWithConfigServiceDecorator(IVirtualDiskDataService decoratedService,
            IConfigWriterService<VirtualDisk> configService, IStateStore stateStore)
        : IVirtualDiskDataService
    {
        public Task<Option<VirtualDisk>> GetVHD(Guid id)
        {
            return decoratedService.GetVHD(id);
        }

        public async Task<VirtualDisk> AddNewVHD(VirtualDisk virtualDisk)
        {
            var result = await decoratedService.AddNewVHD(virtualDisk);
            await configService.Add(virtualDisk, virtualDisk.Project.Name);

            return result;
        }

        public Task<IEnumerable<VirtualDisk>> FindVHDByLocation(
            Guid projectId, string dataStore, string environment, 
            string storageIdentifier, string name, Guid diskIdentifier)
        {
            return decoratedService.FindVHDByLocation(projectId, dataStore, environment, storageIdentifier, name,
                diskIdentifier);
        }
        public Task<IEnumerable<VirtualDisk>> FindOutdated(DateTimeOffset lastSeenBefore, string agentName)
        {
            return decoratedService.FindOutdated(lastSeenBefore, agentName);
        }

        public async Task<VirtualDisk> UpdateVhd(VirtualDisk virtualDisk)
        {
            var res = await decoratedService.UpdateVhd(virtualDisk);
            await configService.Update(res, virtualDisk.Project.Name);

            return res;
        }

        public async Task<Unit> DeleteVHD(Guid id)
        {
            var optionalData = await decoratedService.GetVHD(id);
            await decoratedService.DeleteVHD(id);
            await optionalData.IfSomeAsync(data =>
            {
                stateStore.LoadProperty(data, x=>x.Project);
                return configService.Delete(data, data.Project.Name);
            });

            return Unit.Default;
        }
    }
}