using System.Collections.Generic;
using Dbosoft.Hosuto.Modules.Hosting;
using Dbosoft.Hosuto.Modules.Testing;
using Eryph.ModuleCore;
using Eryph.Security.Cryptography;
using Moq;
using SimpleInjector;

namespace Eryph.Modules.Identity.Test.Integration
{
    public class IdentityModuleFactory : WebModuleFactory<IdentityModule>
    {
        private readonly Container _container = new();
        
        protected override IModulesHostBuilder CreateModuleHostBuilder()
        {
            var moduleHostBuilder = new ModulesHostBuilder();
            _container.Options.AllowOverridingRegistrations = true;
            moduleHostBuilder.UseSimpleInjector(_container);


            var endpoints = new Dictionary<string, string>
            {
                {"identity", "http://localhost/identity"},
                {"compute", "http://localhost/compute"},
                {"common", "http://localhost/common"},
            };

            _container.RegisterInstance<IEndpointResolver>(new EndpointResolver(endpoints));


            var cryptoIOMock = new Mock<ICryptoIOServices>();
            _container.RegisterInstance(cryptoIOMock.Object);

            var certStoreMock = new Mock<ICertificateStoreService>();
            _container.RegisterInstance(certStoreMock.Object);

            var cerGenMock = new Mock<ICertificateGenerator>();
            _container.RegisterInstance(cerGenMock.Object);

            //_container.RegisterInstance(new InMemoryDatabaseRoot());
            //_container
            //    .Register<IDbContextConfigurer<ConfigurationDbContext>, InMemoryConfigurationStoreContextConfigurer>();

            return moduleHostBuilder;
        }

    }
}