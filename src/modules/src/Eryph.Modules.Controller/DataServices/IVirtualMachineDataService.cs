using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Eryph.StateDb.Model;
using LanguageExt;

namespace Eryph.Modules.Controller.DataServices;

internal interface IVirtualMachineDataService
{
    Task<Option<Catlet>> GetByVMId(Guid id);

    Task<Option<Catlet>> GetVM(Guid id);

    Task<Catlet> AddNewVM(Catlet catlet);

    Task<Unit> RemoveVM(Guid id);

    Task<IEnumerable<Catlet>> GetAll();
}
