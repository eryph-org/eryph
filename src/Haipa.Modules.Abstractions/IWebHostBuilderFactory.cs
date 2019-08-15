using Microsoft.AspNetCore.Hosting;

namespace Haipa.Modules
{
    public interface IWebModuleHostBuilderFactory
    {
        IWebHostBuilder CreateWebHostBuilder(string moduleName, string path);

    }

}