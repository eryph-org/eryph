using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Eryph.StateDb.Model;

namespace Eryph.Modules.Controller.DataServices;

internal interface ICatletDataService
{
    Task<Catlet?> GetByVmId(Guid id);

    Task<Catlet?> Get(Guid id);

    Task Add(Catlet catlet);

    Task Remove(Guid id);

    Task<IReadOnlyList<Guid>> GetAllVmIds(string agentName);
}
