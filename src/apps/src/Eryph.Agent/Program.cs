using System.Threading.Tasks;
using Dbosoft.Hosuto.Modules.Hosting;
using Eryph.Modules.HostAgent;
using SimpleInjector;

namespace Eryph.Agent
{
    internal class Program
    {
        private static Task Main(string[] args)
        {
            var container = new Container();
            container.Bootstrap();

            return ModulesHost.CreateDefaultBuilder(args)
                .UseSimpleInjector(container)
                .RunModule<VmHostAgentModule>();
        }
    }
}