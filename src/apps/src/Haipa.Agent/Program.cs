using System.Threading.Tasks;
using Dbosoft.Hosuto.Modules.Hosting;
using Haipa.Modules.VmHostAgent;

using SimpleInjector;


namespace Haipa.Agent
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
