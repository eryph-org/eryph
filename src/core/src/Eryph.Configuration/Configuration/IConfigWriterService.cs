using System.Threading.Tasks;

namespace Eryph.Configuration
{
    public interface IConfigWriterService<in TConfig>
    {
        Task Delete(TConfig config);
        Task Update(TConfig config);
        Task Add(TConfig config);
    }
}