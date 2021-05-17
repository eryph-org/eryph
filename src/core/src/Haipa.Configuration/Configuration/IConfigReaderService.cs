using System.Collections.Generic;

namespace Haipa.Configuration
{
    public interface IConfigReaderService<out TConfig>
    {
        IEnumerable<TConfig> GetConfig();
    }


}