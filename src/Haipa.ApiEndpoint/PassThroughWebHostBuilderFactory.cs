using System;
using Haipa.Modules.Abstractions;
using Microsoft.AspNetCore.Hosting;

namespace Haipa.ApiEndpoint
{
    public class PassThroughWebHostBuilderFactory : IWebModuleHostBuilderFactory
    {
        private readonly Func<IWebHostBuilder> _builder;

        public PassThroughWebHostBuilderFactory(Func<IWebHostBuilder> builder)
        {
            _builder = builder;
        }

        public IWebHostBuilder CreateWebHostBuilder(string moduleName)
        {
            return _builder();
        }
    }
}