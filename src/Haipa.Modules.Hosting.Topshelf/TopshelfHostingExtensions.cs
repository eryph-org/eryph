using System;
using Dbosoft.Hosuto.Modules.Hosting;
using Microsoft.Extensions.Hosting;
using SimpleInjector;
using Topshelf;

namespace Haipa.Modules.Hosting
{
    public static class TopshelfHostingExtensions
    {
        public static void RunModuleHostService(this Container container, string name)
        {
            var rc = HostFactory.Run(config =>
            {
                config.UseSimpleInjector(container);
                config.SetServiceName(name);
                //TODO: we have to move the entire service integration to Hosuto
                //config.Service<ModuleCollectionHost>(s =>
                //{
                //    s.ConstructUsingSimpleInjector();
                //    s.WhenStarted((service, control) => service.Start());
                //    s.WhenStopped((service, control) => service.Stop());
                //});
            });
            var exitCode = (int)Convert.ChangeType(rc, rc.GetTypeCode());
            Environment.ExitCode = exitCode;
        }


    }
}
