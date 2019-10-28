namespace Haipa.Runtime.Zero
{
    using Haipa.Modules.Hosting;
    using Microsoft.Extensions.DependencyInjection;
    using SimpleInjector;
    using System;
    using System.Diagnostics;
    using System.IO;

    /// <summary>
    /// Defines the <see cref="Program" />
    /// </summary>
    internal class Program
    {
        /// <summary>
        /// The Main
        /// </summary>
        /// <param name="args">The args<see cref="string[]"/></param>
        private static void Main(string[] args)
        {
            var container = new Container();
            container.Bootstrap(args);
            #region Identity Server Seeder
            var serviceProvider = new ServiceCollection()
    .AddDbContext<IdentityDb.ConfigurationStoreContext>(options =>
    {
        IdentityDb.IDbContextConfigurer<IdentityDb.ConfigurationStoreContext> configurer = (IdentityDb.IDbContextConfigurer<IdentityDb.ConfigurationStoreContext>)container.GetInstance(typeof(IdentityDb.IDbContextConfigurer<IdentityDb.ConfigurationStoreContext>));
        configurer.Configure(options);
    })
    .AddSingleton<IIdentityServerSeederService, IdentityServerSeederService>()
    .BuildServiceProvider();
            var seederService = serviceProvider.GetService<IIdentityServerSeederService>();
            seederService.Seed();
            #endregion
            container.RunModuleHostService("haipa-zero");
        }
    }
}
