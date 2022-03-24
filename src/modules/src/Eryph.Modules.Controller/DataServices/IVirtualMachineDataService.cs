using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Eryph.StateDb.Model;
using LanguageExt;
using VirtualMachineMetadata = Eryph.Resources.Machines.VirtualMachineMetadata;

namespace Eryph.Modules.Controller.DataServices;

internal interface IVirtualMachineDataService
{
    Task<Option<VirtualMachine>> GetByVMId(Guid id);

    Task<Option<VirtualMachine>> GetVM(Guid id);
    Task<VirtualMachine> AddNewVM(VirtualMachine vm, VirtualMachineMetadata metadata);

    Task<Unit> RemoveVM(Guid id);

    Task<IEnumerable<VirtualMachine>> GetAll();


}

