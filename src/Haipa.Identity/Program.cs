using System;
using System.Threading.Tasks;
using Haipa.Modules.Hosting;
using Haipa.Modules.Identity;
using Haipa.StateDb.MySql;
using SimpleInjector;

namespace Haipa.Identity
{
    public class Program
    {

        public static async Task Main(string[] args)
        {
            var container = new Container();
            container.Bootstrap(args);

            await container.HostModules().RunModule<IdentityModule>((sp) => Task.CompletedTask).ConfigureAwait(false);

        }

    }
}
