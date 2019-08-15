using System;
using Microsoft.AspNetCore.Hosting;

namespace Haipa.Modules.Hosting
{
    public class PassThroughWebHostBuilderFactory : IWebModuleHostBuilderFactory
    {
        private readonly Func<string,IWebHostBuilder> _builder;

        public PassThroughWebHostBuilderFactory(Func<string,IWebHostBuilder> builder)
        {
            _builder = builder;
        }

        public IWebHostBuilder CreateWebHostBuilder(string moduleName, string path)
        {
            return _builder(path);
        }
    }
}