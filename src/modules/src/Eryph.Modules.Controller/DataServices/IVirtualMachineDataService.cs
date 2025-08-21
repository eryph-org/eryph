using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Eryph.Resources.Machines;
using Eryph.StateDb.Model;
using LanguageExt;
using CatletMetadata = Eryph.Resources.Machines.CatletMetadata;

namespace Eryph.Modules.Controller.DataServices;

internal interface IVirtualMachineDataService
{
    Task<Option<Catlet>> GetByVMId(Guid id);

    Task<Option<Catlet>> GetVM(Guid id);
    Task<Catlet> AddNewVM(Catlet catlet, CatletMetadata metadata);

    Task<Unit> RemoveVM(Guid id);

    Task<IEnumerable<Catlet>> GetAll();


}

