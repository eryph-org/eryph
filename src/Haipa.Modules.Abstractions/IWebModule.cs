using System.Threading.Tasks;

namespace Haipa.Modules
{
    public interface IWebModule : IModule
    {
        Task RunAsync();

    }
}