using System.Threading.Tasks;
using Haipa.Modules.Hosting;
using Haipa.Modules.VmHostAgent;

using SimpleInjector;


namespace Haipa.Agent
{
    internal class Program
    {
        private static Task Main(string[] args)
        {
            var container = new Container();
            container.Bootstrap(args);

            return container.HostModules().RunModule<VmHostAgentModule>();
        }
    }
}
