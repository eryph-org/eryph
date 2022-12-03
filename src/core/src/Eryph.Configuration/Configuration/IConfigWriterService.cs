using System.Threading.Tasks;

namespace Eryph.Configuration
{
    public interface IConfigWriterService<in TConfig>
    {
        Task Delete(TConfig config, string projectName);
        Task Update(TConfig config, string projectName);
        Task Add(TConfig config, string projectName);
    }
}