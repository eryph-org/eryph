using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;

namespace Haipa.Modules.Abstractions
{
    public interface IWebModuleHostBuilderFactory
    {
        IWebHostBuilder CreateWebHostBuilder(string moduleName);

    }

}