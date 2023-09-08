using System.Threading.Tasks;
using Eryph.Configuration;
using Eryph.Resources.Machines;

namespace Eryph.Runtime.Zero.Configuration.VMMetadata
{
    internal class VMMetadataConfigWriterService : IConfigWriterService<CatletMetadata>
    {
        private readonly ConfigIO _io;

        public VMMetadataConfigWriterService()
        {
            _io = new ConfigIO(ZeroConfig.GetMetadataConfigPath());
        }

        public Task Delete(CatletMetadata metadata, string projectName)
        {
            _io.DeleteConfigFile(metadata.Id.ToString());
            return Task.CompletedTask;
        }

        public Task Update(CatletMetadata metadata, string projectName)
        {
            return _io.SaveConfigFile(metadata, metadata.Id.ToString());
        }

        public Task Add(CatletMetadata metadata, string projectName)
        {
            return _io.SaveConfigFile(metadata, metadata.Id.ToString());
        }
    }
}