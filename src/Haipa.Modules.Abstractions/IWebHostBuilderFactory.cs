using Microsoft.AspNetCore.Hosting;

namespace Haipa.Modules.Abstractions
{
    public interface IWebModuleHostBuilderFactory
    {
        IWebHostBuilder CreateWebHostBuilder(string moduleName);

    }

}