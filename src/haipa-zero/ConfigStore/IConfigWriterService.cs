using System.Threading.Tasks;

namespace Haipa.Runtime.Zero.ConfigStore.Clients
{
    internal interface IConfigWriterService<in TConfig>
    {
        Task Delete(TConfig config);
        Task Update(TConfig config);
        Task Add(TConfig config);

    }
}