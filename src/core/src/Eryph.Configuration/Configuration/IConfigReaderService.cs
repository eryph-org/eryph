using System.Collections.Generic;

namespace Eryph.Configuration
{
    public interface IConfigReaderService<out TConfig>
    {
        IEnumerable<TConfig> GetConfig();
    }
}