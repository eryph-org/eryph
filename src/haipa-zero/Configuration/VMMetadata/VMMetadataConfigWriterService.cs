using System.Threading.Tasks;
using Haipa.Configuration;
using Haipa.Runtime.Zero.Configuration.Clients;
using Haipa.VmConfig;

namespace Haipa.Runtime.Zero.Configuration.VMMetadata
{
    internal class VMMetadataConfigWriterService : IConfigWriterService<VirtualMachineMetadata>
    {
        private readonly ConfigIO _io;

        public VMMetadataConfigWriterService()
        {
            _io = new ConfigIO(ZeroConfig.GetMetadataConfigPath());
        }

        public Task Delete(VirtualMachineMetadata metadata)
        {
            _io.DeleteConfigFile(metadata.Id.ToString());
            return Task.CompletedTask;
        }

        public Task Update(VirtualMachineMetadata metadata)
        {
            return _io.SaveConfigFile(metadata, metadata.Id.ToString());

        }

        public Task Add(VirtualMachineMetadata metadata)
        {
            return _io.SaveConfigFile(metadata, metadata.Id.ToString());

        }
    }
}