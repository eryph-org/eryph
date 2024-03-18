using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Eryph.Configuration
{
    public interface ISimpleConfigWriter<in TConfig>
    {
        Task Add(TConfig config);

        Task Delete(TConfig config);

        Task Update(TConfig config);
    }
}
