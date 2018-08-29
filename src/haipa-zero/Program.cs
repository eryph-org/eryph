using System;
using SimpleInjector;
using Topshelf;
using Topshelf.SimpleInjector;

namespace Haipa.Runtime.Zero
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            var container = new Container();
            container.Bootstrap(args);

            var rc = HostFactory.Run(config =>
            {
                config.UseSimpleInjector(container);

                config.Service<ModuleHostService>(s =>
                {
                    s.ConstructUsingSimpleInjector();
                    s.WhenStarted((service, control) => service.Start());
                    s.WhenStopped((service, control) => service.Stop());
                });
            });
            var exitCode = (int)Convert.ChangeType(rc, rc.GetTypeCode());
            Environment.ExitCode = exitCode;
        }
    }
}
