using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Eryph.StateDb.Model;
using LanguageExt;

namespace Eryph.Modules.Controller.DataServices;

internal interface IVirtualMachineDataService
{
    Task<Catlet?> GetByVmId(Guid id);

    Task<Option<Catlet>> Get(Guid id);

    Task<Catlet> Add(Catlet catlet);

    Task Remove(Guid id);

    Task<IReadOnlyList<Guid>> GetAllVmIds(string agentName);
}
