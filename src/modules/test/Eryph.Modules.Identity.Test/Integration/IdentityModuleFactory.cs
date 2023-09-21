using System.Collections.Generic;
using Dbosoft.Hosuto.Modules.Hosting;
using Dbosoft.Hosuto.Modules.Testing;
using Eryph.IdentityDb;
using Eryph.ModuleCore;
using Eryph.Modules.Identity.Services;
using Microsoft.EntityFrameworkCore.Storage;
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
                {"identity", "https://localhost/identity"},
                {"compute", "https://localhost/compute"},
                {"common", "https://localhost/common"},
            };

            _container.RegisterInstance<IEndpointResolver>(new EndpointResolver(endpoints));

            _container.RegisterSingleton<ISigningCertificateManager, TestCertificateManager>();

            _container.RegisterInstance(new InMemoryDatabaseRoot());
            _container
                .Register<IDbContextConfigurer<IdentityDbContext>, InMemoryIdentityDbContextConfigurer>();

            return moduleHostBuilder;
        }

    }
}