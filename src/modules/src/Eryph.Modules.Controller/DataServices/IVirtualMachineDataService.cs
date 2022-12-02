using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Eryph.StateDb.Model;
using LanguageExt;
using VirtualMachineMetadata = Eryph.Resources.Machines.VirtualMachineMetadata;

namespace Eryph.Modules.Controller.DataServices;

internal interface IVirtualMachineDataService
{
    Task<Option<VirtualCatlet>> GetByVMId(Guid id);

    Task<Option<VirtualCatlet>> GetVM(Guid id);
    Task<VirtualCatlet> AddNewVM(VirtualCatlet vm, VirtualMachineMetadata metadata);

    Task<Unit> RemoveVM(Guid id);

    Task<IEnumerable<VirtualCatlet>> GetAll();


}

