using System.Collections.Generic;

namespace Haipa.Runtime.Zero.ConfigStore.Clients
{
    internal interface IConfigReaderService<out TConfig>
    {
        IEnumerable<TConfig> GetConfig();
    }


}