using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Eryph.Resources.Machines;
using Eryph.StateDb.Model;
using LanguageExt;

namespace Eryph.Modules.Controller.DataServices;

internal interface IVirtualMachineDataService
{
    Task<Option<VirtualCatlet>> GetByVMId(Guid id);

    Task<Option<VirtualCatlet>> GetVM(Guid id);
    Task<VirtualCatlet> AddNewVM(VirtualCatlet vm, VirtualCatletMetadata metadata);

    Task<Unit> RemoveVM(Guid id);

    Task<IEnumerable<VirtualCatlet>> GetAll();


}

