using Rebus.Config;
using Rebus.Persistence.FileSystem;
using Rebus.Sagas;

namespace Eryph.Rebus;

public class FileSystemSagasConfigurer : IRebusSagasConfigurer
{
        
    public void Configure(StandardConfigurer<ISagaStorage> sagaStorage)
    {
        sagaStorage.UseFilesystem("C:\\ProgramData\\eryph\\zero\\private\\sagas");
    }
}